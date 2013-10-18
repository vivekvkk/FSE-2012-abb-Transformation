﻿/******************************************************************************
 * Copyright (c) 2013 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *  Patrick Francis (ABB Group) - initial API, implementation, & documentation
 *  Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/

using ABB.SrcML.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ABB.SrcML.Data {

    /// <summary>
    /// The data archive is an incrementally updating data container.
    /// </summary>
    /// <example>
    /// <code> var sourceFolder = new FileSystemSourceFolder("path to folder"); var archive = new
    /// SrcMLArchive(sourceFolder);
    ///
    /// // this should generate a data archive for the given srcML archive // when we start
    /// serializing data, we should consider how the location of the data can be stored in the srcmL
    /// archive // calling "DataRepository" on a srcML archive that already has data should
    /// transparently load the existing data DataRepository data = new DataRepository(archive);
    ///
    /// // testDeclaration is some declaration within archive var testDeclaration = new
    /// XElement(SRC.Declaration, "test data");
    ///
    /// // The TypeUse object represents the context that the type is being used in TypeUse
    /// typeUseForDeclaration = new TypeUse(testDeclaration); TypeDefinition typeInfo =
    /// data.ResolveType(typeUseForDeclaration);
    ///
    /// // one of the thing we should be able to do is get the original XML. Behind the scenes, this
    /// relies on using XPath queries // generated by Extensions.GetXPath() XElement typeXml =
    /// typeInfo.GetXElement(); TypeUse parentType = typeInfo.ParentTypes.First(); XElement
    /// parentXml = data.ResolveType(parentType).GetXElement(); </code>
    /// </example>
    public class DataRepository : IDisposable {
        private IScope globalScope;
        private Dictionary<Language, AbstractCodeParser> parsers;
        private ReadyNotifier ReadyState;

        /// <summary>
        /// Create a data archive for the given srcML archive. It will subscribe to the
        /// <see cref="AbstractArchive.FileChanged"/> event.
        /// </summary>
        /// <param name="archive">The archive to monitor for changes.</param>
        public DataRepository(ISrcMLArchive archive)
            : this(archive, null) {
        }

        /// <summary>
        /// Create a data archive with data stored in the given
        /// <paramref name="fileName">binary file</paramref> .
        /// </summary>
        /// <param name="fileName">The binary file the data archive is stored in</param>
        public DataRepository(string fileName)
            : this(null, fileName) {
        }

        /// <summary>
        /// Create a data archive for the given srcML archive and binary file. It will load data
        /// from the binary archive and then subscribe to the srcML archive.
        /// </summary>
        /// <param name="archive">The srcML archive to monitor for changes. If null, no archive
        /// monitoring will be done.</param>
        /// <param name="fileName">The file to read data from. If null, no previously saved data
        /// will be loaded.</param>
        public DataRepository(ISrcMLArchive archive, string fileName) {
            SetupParsers();
            this.ReadyState = new ReadyNotifier(this);
            this.Archive = archive;
            this.FileName = fileName;

            bool loadedFromDisk = false;

            if(FileName != null) {
                if(File.Exists(this.FileName)) {
                    Load(this.FileName);
                    loadedFromDisk = true;
                }
            }

            if(this.Archive != null) {
                this.Archive.FileChanged += Archive_SourceFileChanged;
                if(!loadedFromDisk) {
                    InitializeData();
                }
            }
        }

        /// <summary>
        /// Raised whenever an expected error is raised
        /// </summary>
        public event EventHandler<ErrorRaisedArgs> ErrorRaised;

        /// <summary>
        /// The file processed event is raised once the data repository is done parsing a file and
        /// merging the results
        /// </summary>
        public event EventHandler<FileEventRaisedArgs> FileProcessed;

        /// <summary>
        /// Raises whenever the value of <see cref="IsReady"/> changes
        /// </summary>
        public event EventHandler<IsReadyChangedEventArgs> IsReadyChanged {
            add { this.ReadyState.IsReadyChanged += value; }
            remove { this.ReadyState.IsReadyChanged -= value; }
        }

        /// <summary>
        /// The SrcMLArchive to extract the data from.
        /// </summary>
        public ISrcMLArchive Archive { get; private set; }

        /// <summary>
        /// The file name to serialize to
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// The top-level Scope for the data in the archive.
        /// </summary>
        public IScope GlobalScope { get { return globalScope; } }

        /// <summary>
        /// True if this repository is idle; false if it is responding to file updates
        /// </summary>
        public bool IsReady {
            get { return ReadyState.IsReady; }
            private set { this.ReadyState.IsReady = value; }
        }

        #region Modification methods

        /// <summary>
        /// Adds the given file to the data archive.
        /// </summary>
        /// <param name="sourceFile">The path of the file to add.</param>
        public void AddFile(string sourceFile) {
            var unit = Archive.GetXElementForSourceFile(sourceFile);
            AddFile(unit);
        }

        /// <summary>
        /// Adds the given file to the data archive.
        /// </summary>
        /// <param name="fileUnitElement">The <see cref="SRC.Unit"/> XElement for the file to
        /// add.</param>
        public void AddFile(XElement fileUnitElement) {
            bool wasIdle = IsReady;
            if(wasIdle) {
                IsReady = false;
            }
            var scope = ParseFileUnit(fileUnitElement);
            if(scope != null) {
                MergeScope(scope);
            }
            if(wasIdle) {
                IsReady = true;
            }
            //TODO: update other data structures as necessary
        }

        /// <summary>
        /// Removes any data from the archive.
        /// </summary>
        public void Clear() {
            globalScope = null;
            //TODO: clear any other data structures as necessary
        }

        /// <summary>
        /// Removes the given file from the data archive
        /// </summary>
        /// <param name="sourceFile">The path of the file to remove.</param>
        public void RemoveFile(string sourceFile) {
            IsReady = false;
            globalScope.RemoveFile(sourceFile);
            IsReady = true;
        }

        #endregion Modification methods

        #region Query methods

        /// <summary>
        /// Returns the method calls at the given source location. These are sorted with the calls
        /// closest to the location appearing first.
        /// </summary>
        /// <param name="loc">The source location to search for.</param>
        /// <returns>A collection of the method calls at the given location.</returns>
        public Collection<IMethodCall> FindMethodCalls(SourceLocation loc) {
            if(loc == null)
                throw new ArgumentNullException("loc");
            var scope = globalScope.GetScopeForLocation(loc);
            if(scope == null) {
                //TODO replace logger call
                //Utilities.SrcMLFileLogger.DefaultLogger.InfoFormat("SourceLocation {0} not found in DataRepository", loc);
                return new Collection<IMethodCall>();
            }
            var calls = scope.MethodCalls.Where(mc => mc.Location.Contains(loc));
            return new Collection<IMethodCall>(calls.OrderByDescending(mc => mc.Location, new SourceLocationComparer()).ToList());
        }

        /// <summary>
        /// Returns the method calls surrounding the given srcML element. These are sorted with the
        /// calls closest to the element appearing first.
        /// </summary>
        /// <param name="element">The XElement to search for.</param>
        /// <returns>A collection of the method calls at the given element.</returns>
        public Collection<IMethodCall> FindMethodCalls(XElement element) {
            if(element == null)
                throw new ArgumentNullException("element");
            return FindMethodCalls(element.GetXPath(false));
        }

        /// <summary>
        /// Returns the method calls at the given source location. These are sorted with the calls
        /// closest to the location appearing first.
        /// </summary>
        /// <param name="xpath">The path to search for.</param>
        /// <returns>A collection of the method calls at the given path.</returns>
        public Collection<IMethodCall> FindMethodCalls(string xpath) {
            if(xpath == null)
                throw new ArgumentNullException("xpath");
            var scope = globalScope.GetScopeForLocation(xpath);
            var calls = scope.MethodCalls.Where(mc => xpath.StartsWith(mc.Location.XPath));
            return new Collection<IMethodCall>(calls.OrderByDescending(mc => mc.Location, new SourceLocationComparer()).ToList());
        }

        /// <summary>
        /// Finds the innermost scope that contains the given source location.
        /// </summary>
        /// <param name="loc">The source location to search for.</param>
        /// <returns>The innermost scope containing the location, or null if it is not
        /// found.</returns>
        public IScope FindScope(SourceLocation loc) {
            return globalScope.GetScopeForLocation(loc);
        }

        /// <summary>
        /// Finds the innermost scope that contains the given element.
        /// </summary>
        /// <param name="element">The element to search for.</param>
        /// <returns>The innermost scope containing the element, or null if it is not
        /// found.</returns>
        public IScope FindScope(XElement element) {
            return globalScope.GetScopeForLocation(element.GetXPath(false));
        }

        /// <summary>
        /// Finds the innermost scope that contains the given XPath.
        /// </summary>
        /// <param name="xpath">The XPath to search for.</param>
        /// <returns>The innermost scope containing the XPath, or null if it is not found.</returns>
        public IScope FindScope(string xpath) {
            return globalScope.GetScopeForLocation(xpath);
        }

        #endregion Query methods

        #region initialization methods

        /// <summary>
        /// Initializes the archive. If <see cref="FileName"/> is set and exists, then it attempts
        /// to read the data from disk via <see cref="Load(string)"/>. If the load fails the
        /// repository raises an <see cref="ErrorRaised"/> event and then iterates over all of the
        /// file units in <see cref="Archive"/>.
        /// </summary>
        public void InitializeData() {
            IsReady = false;
            Clear();
            if(FileName != null && File.Exists(FileName)) {
                try {
                    Load(FileName);
                } catch(SerializationException e) {
                    OnErrorRaised(new ErrorRaisedArgs(e));
                }
            }
            if(globalScope == null) {
                ReadArchive();
            }
            IsReady = true;
        }

        /// <summary>
        /// Initializes the archive. If <see cref="FileName"/> is set and exists, then it attempts
        /// to read the data from disk via <see cref="Load(string)"/>. If the load fails the
        /// repository raises an <see cref="ErrorRaised"/> event and then iterates over all of the
        /// file units in <see cref="Archive"/>. It parses the file units concurrently and merges
        /// them on the main thread.
        /// </summary>
        public void InitializeDataConcurrent() {
            InitializeDataConcurrent(TaskScheduler.Default);
        }

        /// <summary>
        /// initializes the archive. If <see cref="FileName"/> is set and exists, then it attempts
        /// to read the data from disk via <see cref="Load(string)"/>. If the load fails the
        /// repository raises an <see cref="ErrorRaised"/> event and then iterates over all of the
        /// file units in <see cref="Archive"/>. It parses the file units concurrently and merges
        /// them on the main thread.
        /// </summary>
        /// <param name="scheduler">The scheduler to use</param>
        public void InitializeDataConcurrent(TaskScheduler scheduler) {
            IsReady = false;
            Clear();
            if(FileName != null && File.Exists(FileName)) {
                try {
                    Load(FileName);
                } catch(SerializationException e) {
                    OnErrorRaised(new ErrorRaisedArgs(e));
                }
            }
            if(globalScope == null) {
                ReadArchiveConcurrent(scheduler);
            }
            IsReady = true;
        }

        /// <summary>
        /// Initializes the archive from the given file. This file must be a serialized
        /// DataRepository produced by DataRepository.Save().
        /// </summary>
        /// <param name="fileName">The file to load the archive from.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">A problem occurred
        /// in deserialization. E.g. the serialized data is the wrong version.</exception>
        public void Load(string fileName) {
            if(fileName == null) {
                throw new ArgumentNullException("fileName");
            }
            using(var f = File.OpenRead(fileName)) {
                var formatter = new BinaryFormatter();
                var tempScope = (Scope) formatter.Deserialize(f);
                //Will throw an exception if it doesn't deserialize correctly
                this.FileName = fileName;
                this.globalScope = tempScope;
                SetupParsers();
            }
        }

        #endregion initialization methods

        #region teardown methods

        /// <summary>
        /// Disposes of the repository
        /// </summary>
        public void Dispose() {
            if(this.Archive != null) {
                this.Archive.FileChanged -= Archive_SourceFileChanged;
            }
            this.FileProcessed = null;
            this.ErrorRaised = null;
            ReadyState.Dispose();
            Save();
        }

        /// <summary>
        /// Serializes the archive to the specified file.
        /// </summary>
        /// <param name="fileName">The file to save the archive to.</param>
        public void Save(string fileName) {
            if(fileName == null) {
                throw new ArgumentNullException("fileName");
            }
            var formatter = new BinaryFormatter();
            using(var f = File.OpenWrite(fileName)) {
                formatter.Serialize(f, globalScope);
            }
            this.FileName = fileName;
        }

        /// <summary>
        /// Serializes the archive to <see cref="FileName"/>
        /// </summary>
        public void Save() {
            if(this.FileName != null) {
                Save(this.FileName);
            }
        }

        #endregion teardown methods

        #region Private Methods

        private void Archive_SourceFileChanged(object sender, FileEventRaisedArgs e) {
            try {
                switch(e.EventType) {
                    case FileEventType.FileChanged:
                        // Treat a changed source file as deleted then added
                        RemoveFile(e.FilePath);
                        goto case FileEventType.FileAdded;
                    case FileEventType.FileAdded:
                        AddFile(e.FilePath);
                        break;

                    case FileEventType.FileDeleted:
                        RemoveFile(e.FilePath);
                        break;

                    case FileEventType.FileRenamed:
                        // TODO: could a more efficient rename action be supported within the data
                        //       structures themselves?
                        RemoveFile(e.OldFilePath);
                        AddFile(e.FilePath);
                        break;
                }
                OnFileProcessed(e);
            } catch(Exception ex) {
                // TODO log exception
                Console.Error.WriteLine("Error: {0} ({1} {2})", ex.Message, e.EventType, e.FilePath);
            }
        }

        private void MergeScope(IScope scopeForFile) {
            globalScope = (globalScope != null ? globalScope.Merge(scopeForFile) : scopeForFile);
        }

        private void OnErrorRaised(ErrorRaisedArgs e) {
            EventHandler<ErrorRaisedArgs> handler = ErrorRaised;
            if(handler != null) {
                handler(this, e);
            }
        }

        private void OnFileProcessed(FileEventRaisedArgs e) {
            EventHandler<FileEventRaisedArgs> handler = FileProcessed;
            if(handler != null) {
                handler(this, e);
            }
        }

        private IScope ParseFileUnit(XElement fileUnit) {
            var language = SrcMLElement.GetLanguageForUnit(fileUnit);
            IScope scope = null;
            AbstractCodeParser parser;

            if(parsers.TryGetValue(language, out parser)) {
                try {
                    scope = parser.ParseFileUnit(fileUnit);
                } catch(ParseException e) {
                    OnErrorRaised(new ErrorRaisedArgs(e));
                }
            }
            return scope;
        }

        private void ReadArchive() {
            if(null != Archive) {
                foreach(var unit in Archive.FileUnits) {
                    try {
                        string fileName = SrcMLElement.GetFileNameForUnit(unit);
                        AddFile(unit);
                        OnFileProcessed(new FileEventRaisedArgs(FileEventType.FileAdded, fileName));
                    } catch(Exception ex) {
                        OnErrorRaised(new ErrorRaisedArgs(ex));
                    }
                }
            }
        }

        private void ReadArchiveConcurrent(TaskScheduler scheduler) {
            if(null != Archive) {
                BlockingCollection<IScope> mergeQueue = new BlockingCollection<IScope>();

                var task = new Task(() => {
                    Parallel.ForEach(Archive.FileUnits, currentUnit => {
                        var scope = ParseFileUnit(currentUnit);
                        if(scope != null) {
                            mergeQueue.Add(scope);
                        }
                    });
                    mergeQueue.CompleteAdding();
                });

                task.Start(scheduler);

                foreach(var scope in mergeQueue.GetConsumingEnumerable()) {
                    var fileName = scope.PrimaryLocation.SourceFileName;
                    MergeScope(scope);
                    OnFileProcessed(new FileEventRaisedArgs(FileEventType.FileAdded, fileName));
                }
            }
        }

        private void SetupParsers() {
            parsers = new Dictionary<Language, AbstractCodeParser>() {
                { Language.C, new CPlusPlusCodeParser() },
                { Language.CPlusPlus, new CPlusPlusCodeParser() },
                { Language.Java, new JavaCodeParser() },
                { Language.CSharp, new CSharpCodeParser() }
            };
        }

        #endregion Private Methods

        private class SourceLocationComparer : Comparer<SourceLocation> {

            public override int Compare(SourceLocation x, SourceLocation y) {
                if(object.Equals(x, y))
                    return 0;
                if(x == null && y != null)
                    return -1;
                if(x != null && y == null)
                    return 1;

                var result = x.StartingLineNumber.CompareTo(y.StartingLineNumber);
                if(result == 0) {
                    result = x.StartingColumnNumber.CompareTo(y.StartingColumnNumber);
                }
                return result;
            }
        }
    }
}