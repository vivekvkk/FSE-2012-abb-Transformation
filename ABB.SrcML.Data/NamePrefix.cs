﻿/******************************************************************************
 * Copyright (c) 2014 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *    Patrick Francis (ABB Group) - API, implementation, & documentation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ABB.SrcML.Data {
    public class NamePrefix : Expression {
        /// <summary>
        /// The XML name for NamePrefix
        /// </summary>
        public new const string XmlName = "Prefix";

        public NamePrefix() : base() { }

        public IEnumerable<NameUse> Names { get { return Components.OfType<NameUse>(); } }

        public IEnumerable<NamedScope> FindMatches(NamedScope root) {
            var prefixes = Names.ToList();
            Dictionary<NameUse, List<NamedScope>> prefixMap = new Dictionary<NameUse, List<NamedScope>>();
            for(int i = 0; i < prefixes.Count; i++) {
                if(0 == i) {
                    prefixMap[prefixes[i]] = (from child in root.ChildStatements.OfType<NamedScope>()
                                              where child.Name == prefixes[i].Name
                                              select child).ToList();
                } else {
                    prefixMap[prefixes[i]] = (from candidate in prefixMap[prefixes[i - 1]]
                                              from child in candidate.ChildStatements.OfType<NamedScope>()
                                              where child.Name == prefixes[i].Name
                                              select child).ToList();
                }
            }
            return prefixMap[prefixes[prefixes.Count - 1]];
        }

        /// <summary>
        /// Instance method for getting <see cref="NamePrefix.XmlName"/>
        /// </summary>
        /// <returns>Returns the XML name for NamePrefix</returns>
        public override string GetXmlName() { return NamePrefix.XmlName; }

        /// <summary> Returns a string representation of this object. </summary>
        public override string ToString() {
            return string.Join("", Components);
        }
    }
}
