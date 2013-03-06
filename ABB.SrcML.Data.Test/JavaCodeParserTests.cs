﻿/******************************************************************************
 * Copyright (c) 2013 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;

namespace ABB.SrcML.Data.Test {
    class JavaCodeParserTests {
        private string srcMLFormat;
        private AbstractCodeParser codeParser;
        private SrcMLFileUnitSetup fileSetup;

        [TestFixtureSetUp]
        public void ClassSetup() {
            srcMLFormat = SrcMLFileUnitSetup.CreateFileUnitTemplate();
            codeParser = new JavaCodeParser();
            fileSetup = new SrcMLFileUnitSetup(Language.Java);
        }

        [Test]
        public void TestCreateTypeDefinitions_Class() {
            // class A {
            // }
            string xml = @"<class>class <name>A</name> <block>{
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            var globalNamespace = actual.ParentScope as NamespaceDefinition;
            Assert.AreEqual("A", actual.Name);
            Assert.That(globalNamespace.IsGlobal);
        }

        [Test]
        public void TestCreateTypeDefinitions_Interface() {
            // interface A {
            // }
            string xml = @"<class type=""interface"">interface <name>A</name> <block>{
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            var globalNamespace = actual.ParentScope as NamespaceDefinition;

            Assert.AreEqual("A", actual.Name);
            Assert.AreEqual(TypeKind.Interface, actual.Kind);
            Assert.That(globalNamespace.IsGlobal);
        }

        [Test]
        public void TestCreateTypeDefinition_ClassInPackage() {
            // package A.B.C
            // public class D {
            // }
            string xml = @"<package>package <name>A</name>.<name>B</name>.<name>C</name>;</package>

<class><specifier>public</specifier> class <name>D</name> <block>{
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "D.java");

            var globalScope = codeParser.ParseFileUnit(xmlElement);
            var packageABC = globalScope.ChildScopes.First() as NamespaceDefinition;
            var typeD = packageABC.ChildScopes.First() as TypeDefinition;

            Assert.AreEqual("A.B.C", packageABC.Name);
            Assert.IsFalse(packageABC.IsGlobal);

            Assert.AreEqual("D", typeD.Name);
            Assert.IsFalse(packageABC.IsGlobal);
        }

        [Test]
        public void TestCreateTypeDefinitions_ClassWithParents() {
            // class A implements B,C,D {
            // }
            string xml = @"<class>class <name>A</name> <super><implements>implements <name>B</name>,<name>C</name>,<name>D</name></implements></super> <block>{
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            var globalNamespace = actual.ParentScope as NamespaceDefinition;
            Assert.AreEqual("A", actual.Name);
            Assert.AreEqual(3, actual.ParentTypes.Count);
            Assert.That(globalNamespace.IsGlobal);

            var parentNames = from parent in actual.ParentTypes
                              select parent.Name;

            var tests = Enumerable.Zip<string, string, bool>(new[] { "B", "C", "D" }, parentNames, (e, a) => e == a);
            foreach(var test in tests) {
                Assert.That(test);
            }
        }

        [Test]
        public void TestCreateTypeDefinitions_ClassWithQualifiedParent() {
            // class D implements A.B.C {
            // }
            string xml = @"<class>class <name>D</name> <super><implements>implements <name>A</name><op:operator>.</op:operator><name>B</name><op:operator>.</op:operator><name>C</name></implements></super> <block>{
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "D.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            var globalNamespace = actual.ParentScope as NamespaceDefinition;

            Assert.AreEqual("D", actual.Name);
            Assert.AreEqual(1, actual.ParentTypes.Count);
            Assert.That(globalNamespace.IsGlobal);

            var parent = actual.ParentTypes.First();

            Assert.AreEqual("C", parent.Name);
            TestHelper.VerifyPrefixValues(new[] { "A", "B" }, parent.Prefix);
        }

        //TODO: add tests for classes with extends keyword
        [Test]
        public void TestCreateTypeDefinitions_ClassWithSuperClass() {
            //Foo.java
            //public class Foo extends xyzzy {
            //    public int bar;
            //}
            string xml = @"<class><specifier>public</specifier> class <name>Foo</name> <super><extends>extends <name>xyzzy</name></extends></super> <block>{
    <decl_stmt><decl><type><specifier>public</specifier> <name>int</name></type> <name>bar</name></decl>;</decl_stmt>
}</block></class>";
            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "Foo.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            Assert.IsNotNull(actual);
            Assert.AreEqual("Foo", actual.Name);
            Assert.AreEqual(1, actual.ParentTypes.Count);
            Assert.AreEqual("xyzzy", actual.ParentTypes.First().Name);
            var globalNamespace = actual.ParentScope as NamespaceDefinition;
            Assert.IsNotNull(globalNamespace);
            Assert.That(globalNamespace.IsGlobal);
        }

        [Test]
        public void TestCreateTypeDefinitions_ClassWithExtendsAndImplements() {
            //Foo.java
            //public class Foo extends xyzzy implements A, B, C {
            //    public int bar;
            //}
            string xml = @"<class><specifier>public</specifier> class <name>Foo</name> <super><extends>extends <name>xyzzy</name></extends> <implements>implements <name>A</name>, <name>B</name>, <name>C</name></implements></super> <block>{
    <decl_stmt><decl><type><specifier>public</specifier> <name>int</name></type> <name>bar</name></decl>;</decl_stmt>
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "Foo.java");

            var actual = codeParser.ParseFileUnit(xmlElement).ChildScopes.First() as TypeDefinition;
            Assert.IsNotNull(actual);
            var globalNamespace = actual.ParentScope as NamespaceDefinition;
            Assert.IsNotNull(globalNamespace);
            Assert.AreEqual("Foo", actual.Name);
            Assert.AreEqual(4, actual.ParentTypes.Count);
            Assert.That(globalNamespace.IsGlobal);

            var parentNames = from parent in actual.ParentTypes
                              select parent.Name;

            var tests = Enumerable.Zip<string, string, bool>(new[] { "xyzzy", "A", "B", "C" }, parentNames, (e, a) => e == a);
            foreach(var test in tests) {
                Assert.That(test);
            }
        }
        
        [Test]
        public void TestCreateTypeDefinitions_ClassWithInnerClass() {
            // class A {
            //     class B {
            //     }
            // }
            string xml = @"<class>class <name>A</name> <block>{
	<class>class <name>B</name> <block>{
	}</block></class>
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var globalScope = codeParser.ParseFileUnit(xmlElement);

            var typeA = globalScope.ChildScopes.First() as TypeDefinition;
            var typeB = typeA.ChildScopes.First() as TypeDefinition;

            Assert.AreSame(typeA, typeB.ParentScope);
            Assert.AreEqual("A", typeA.FullName);
            Assert.AreEqual("A.B", typeB.FullName);
        }

        [Test]
        public void TestCreateTypeDefinitions_InnerClassWithNamespace() {
            // package A;
            // class B {
            //     class C {
            //     }
            // }
            string xml = @"<package>package <name>A</name>;</package>
<class>class <name>B</name> <block>{
	<class>class <name>C</name> <block>{
	}</block></class>
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "B.java");
            var scopes = VariableScopeIterator.Visit(codeParser.ParseFileUnit(xmlElement));
            var typeDefinitions = from scope in scopes
                                  let definition = (scope as TypeDefinition)
                                  where definition != null
                                  select definition;

            Assert.AreEqual(2, typeDefinitions.Count());

            var outer = typeDefinitions.First() as TypeDefinition;
            var inner = typeDefinitions.Last() as TypeDefinition;

            Assert.AreEqual("B", outer.Name);
            Assert.AreEqual("A", outer.NamespaceName);
            Assert.AreEqual("A.B", outer.FullName);

            Assert.AreEqual("C", inner.Name);
            Assert.AreEqual("A", inner.NamespaceName);
            Assert.AreEqual("A.B.C", inner.FullName);
        }

        [Test]
        public void TestCreateTypeDefinitions_ClassInFunction() {
            // class A {
            //     int foo() {
            //         class B {
            //         }
            //     }
            // }
            string xml = @"<class>class <name>A</name> <block>{
	<function><type><name>int</name></type> <name>foo</name><parameter_list>()</parameter_list> <block>{
		<class>class <name>B</name> <block>{
		}</block></class>
}</block></function>
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var globalScope = codeParser.ParseFileUnit(xmlElement);

            var typeA = globalScope.ChildScopes.First() as TypeDefinition;
            var fooMethod = typeA.ChildScopes.First() as MethodDefinition;
            var typeB = fooMethod.ChildScopes.First() as TypeDefinition;

            Assert.AreEqual("A", typeA.FullName);
            Assert.AreSame(typeA, fooMethod.ParentScope);
            Assert.AreEqual("A.foo", fooMethod.FullName);
            Assert.AreSame(fooMethod, typeB.ParentScope);
            Assert.AreEqual("A.foo.B", typeB.FullName);
        }

        [Test]
        public void TestCreateAliasesForFiles_ImportNamespace() {
            // import x . /*test */ y . z . /*test */  * /*test*/;
            string xml = @"<import>import <name>x</name> . <comment type=""block"">/*test */</comment> <name>y</name> . <name>z</name> . <comment type=""block"">/*test */</comment>  * <comment type=""block"">/*test*/</comment>;</import>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");
            
            var aliases = codeParser.CreateAliasesForFile(xmlElement);

            var actual = aliases.First();

            Assert.AreEqual("x", actual.ImportedNamespace.Name);
            Assert.AreEqual("y", actual.ImportedNamespace.ChildScopeUse.Name);
            Assert.AreEqual("z", actual.ImportedNamespace.ChildScopeUse.ChildScopeUse.Name);
            Assert.IsNull(actual.ImportedNamespace.ChildScopeUse.ChildScopeUse.ChildScopeUse);
            Assert.IsNull(actual.ImportedNamedScope);
            Assert.That(actual.IsNamespaceAlias);
        }

        [Test]
        public void TestCreateAliasesForFiles_ImportClass() {
            //import x.y.z;
            string xml = @"<import>import <name>x</name>.<name>y</name>.<name>z</name>;</import>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var aliases = codeParser.CreateAliasesForFile(xmlElement);

            var actual = aliases.First();

            Assert.AreEqual("x", actual.ImportedNamespace.Name);
            Assert.AreEqual("y", actual.ImportedNamespace.ChildScopeUse.Name);
            Assert.IsNull(actual.ImportedNamespace.ChildScopeUse.ChildScopeUse);

            Assert.AreEqual("z", actual.ImportedNamedScope.Name);
            Assert.IsFalse(actual.IsNamespaceAlias);
        }

        [Test]
        public void TestCreateVariableDeclaration() {
            // class A {
            //     private int X;
            //     int GetX() { return X; }
            // }
            string xml = @"<class>class <name>A</name> <block>{
	<decl_stmt><decl><type><specifier>private</specifier> <name>int</name></type> <name>X</name></decl>;</decl_stmt>
	<function><type><specifier>public</specifier> <name>int</name></type> <name>GetX</name><parameter_list>()</parameter_list> <block>{ <return>return <expr><name>X</name></expr>;</return> }</block></function>
}</block></class>";

            XElement xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var declarationElement = xmlElement.Descendants(SRC.DeclarationStatement).First();

            var rootScope = codeParser.ParseFileUnit(xmlElement);
            var scope = VariableScopeIterator.Visit(rootScope).Last();
            var useOfX = xmlElement.Descendants(SRC.Return).First().Descendants(SRC.Name).First();

            Assert.AreEqual(3, rootScope.GetScopesForPath(useOfX.GetXPath(false)).Count());

            var matchingDeclarations = rootScope.GetDeclarationsForVariableName("X", useOfX.GetXPath(false));
            var declaration = matchingDeclarations.First();

            Assert.AreEqual("int", declaration.VariableType.Name);
            Assert.AreEqual("X", declaration.Name);

            Assert.That(useOfX.GetXPath(false).StartsWith(declaration.Scope.PrimaryLocation.XPath));
        }

        [Test]
        public void TestFieldCreation() {
            // # A.java
            // class A {
            //     int B;
            // }
            string xml = @"<class>class <name>A</name> <block>{
    <decl_stmt><decl><type><name>int</name></type> <name>B</name></decl>;</decl_stmt>
}</block></class>";

            var xmlElement = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var globalScope = codeParser.ParseFileUnit(xmlElement);

            var typeA = globalScope.ChildScopes.First();

            Assert.AreEqual(1, typeA.DeclaredVariables.Count());

            var fieldB = typeA.DeclaredVariables.First();
            Assert.AreEqual("B", fieldB.Name);
            Assert.AreEqual("int", fieldB.VariableType.Name);
        }
        [Test]
        public void TestMethodCallCreation() {
            // # A.java
            // class A {
            //  public int Execute() {
            //      B b = new B();
            //      for(int i = 0; i < b.max(); i++) {
            //          try {
            //              PrintOutput(b.analyze(i));
            //          } catch(Exception e) {
            //              PrintError(e.ToString());
            //          }
            //      }
            //  }
            // }
            string xml = @"<class>class <name>A</name> <block>{
<function><type><specifier>public</specifier> <name>int</name></type> <name>Execute</name><parameter_list>()</parameter_list> <block>{
    <decl_stmt><decl><type><name>B</name></type> <name>b</name> =<init> <expr><op:operator>new</op:operator> <call><name>B</name><argument_list>()</argument_list></call></expr></init></decl>;</decl_stmt>
    <for>for(<init><decl><type><name>int</name></type> <name>i</name> =<init> <expr><lit:literal type=""number"">0</lit:literal></expr></init></decl>;</init> <condition><expr><name>i</name> <op:operator>&lt;</op:operator> <call><name><name>b</name><op:operator>.</op:operator><name>max</name></name><argument_list>()</argument_list></call></expr>;</condition> <incr><expr><name>i</name><op:operator>++</op:operator></expr></incr>) <block>{
        <try>try <block>{
            <expr_stmt><expr><call><name>PrintOutput</name><argument_list>(<argument><expr><call><name><name>b</name><op:operator>.</op:operator><name>analyze</name></name><argument_list>(<argument><expr><name>i</name></expr></argument>)</argument_list></call></expr></argument>)</argument_list></call></expr>;</expr_stmt>
        }</block> <catch>catch(<param><decl><type><name>Exception</name></type> <name>e</name></decl></param>) <block>{
            <expr_stmt><expr><call><name>PrintError</name><argument_list>(<argument><expr><call><name><name>e</name><op:operator>.</op:operator><name>ToString</name></name><argument_list>()</argument_list></call></expr></argument>)</argument_list></call></expr>;</expr_stmt>
        }</block></catch></try>
    }</block></for>
}</block></function>
}</block></class>";

            var fileUnit = fileSetup.GetFileUnitForXmlSnippet(xml, "A.java");

            var globalScope = codeParser.ParseFileUnit(fileUnit);

            var executeMethod = globalScope.ChildScopes.First().ChildScopes.First() as MethodDefinition;

            var callToNewB = executeMethod.MethodCalls.First();
            Assert.AreEqual("B", callToNewB.Name);
            Assert.IsTrue(callToNewB.IsConstructor);
            Assert.IsFalse(callToNewB.IsDestructor);

            var forStatement = executeMethod.ChildScopes.First();
            var callToMax = forStatement.MethodCalls.First();
            Assert.AreEqual("max", callToMax.Name);
            Assert.IsFalse(callToMax.IsDestructor);
            Assert.IsFalse(callToMax.IsConstructor);

            var forBlock = forStatement.ChildScopes.First();
            var tryStatement = forBlock.ChildScopes.First();
            var tryBlock = tryStatement.ChildScopes.First();

            var callToPrintOutput = tryBlock.MethodCalls.First();
            Assert.AreEqual("PrintOutput", callToPrintOutput.Name);

            var callToAnalyze = tryBlock.MethodCalls.Last();
            Assert.AreEqual("analyze", callToAnalyze.Name);

            var catchStatement = tryStatement.ChildScopes.Last();
            var catchBlock = catchStatement.ChildScopes.First();
            
            var callToPrintError = catchBlock.MethodCalls.First();
            Assert.AreEqual("PrintError", callToPrintError.Name);

            var callToToString = catchBlock.MethodCalls.Last();
            Assert.AreEqual("ToString", callToToString.Name);
        }

        [Test]
        public void TestMethodCallCreation_WithThisKeyword() {
            //class A {
            //    void Bar() { }
            //    class B {
            //        int a;
            //        void Foo() { this.Bar(); }
            //        void Bar() { return this.a; }
            //    }
            //}
            string a_xml = @"<class>class <name>A</name> <block>{
    <function><type><name>void</name></type> <name>Bar</name><parameter_list>()</parameter_list> <block>{ }</block></function>
    <class>class <name>B</name> <block>{
        <decl_stmt><decl><type><name>int</name></type> <name>a</name></decl>;</decl_stmt>
        <function><type><name>void</name></type> <name>Foo</name><parameter_list>()</parameter_list> <block>{ <expr_stmt><expr><call><name><name>this</name><op:operator>.</op:operator><name>Bar</name></name><argument_list>()</argument_list></call></expr>;</expr_stmt> }</block></function>
        <function><type><name>void</name></type> <name>Bar</name><parameter_list>()</parameter_list> <block>{ <return>return <expr><name>this</name><op:operator>.</op:operator><name>a</name></expr>;</return> }</block></function>
    }</block></class>
}</block></class>";

            var fileUnit = fileSetup.GetFileUnitForXmlSnippet(a_xml, "A.java");
            var globalScope = codeParser.ParseFileUnit(fileUnit);

            var aDotBar = globalScope.ChildScopes.First().ChildScopes.First() as MethodDefinition;
            var aDotBDotFoo = globalScope.ChildScopes.First().ChildScopes.Last().ChildScopes.First() as MethodDefinition;
            var aDotBDotBar = globalScope.ChildScopes.First().ChildScopes.Last().ChildScopes.Last() as MethodDefinition;

            Assert.AreEqual("A.Bar", aDotBar.FullName);
            Assert.AreEqual("A.B.Foo", aDotBDotFoo.FullName);
            Assert.AreEqual("A.B.Bar", aDotBDotBar.FullName);

            Assert.AreSame(aDotBDotBar, aDotBDotFoo.MethodCalls.First().FindMatches().First());
            Assert.AreEqual(1, aDotBDotFoo.MethodCalls.First().FindMatches().Count());
        }

        [Test]
        public void TestMethodCallCreation_WithConflictingMethodNames() {
            //# A.java
            //class A {
            //    B b;
            //    boolean Contains() { b.Contains(); }
            //}
            string a_xml = @"<class>class <name>A</name> <block>{
    <decl_stmt><decl><type><name>B</name></type> <name>b</name></decl>;</decl_stmt>
    <function><type><name>boolean</name></type> <name>Contains</name><parameter_list>()</parameter_list> <block>{ <expr_stmt><expr><call><name><name>b</name><op:operator>.</op:operator><name>Contains</name></name><argument_list>()</argument_list></call></expr>;</expr_stmt> }</block></function>
}</block></class>";

            //class B {
            //    boolean Contains() { return true; }
            //}
            string b_xml = @"<class>class <name>B</name> <block>{
    <function><type><name>boolean</name></type> <name>Contains</name><parameter_list>()</parameter_list> <block>{ <return>return <expr><lit:literal type=""boolean"">true</lit:literal></expr>;</return> }</block></function>
}</block></class>";

            var fileUnitA = fileSetup.GetFileUnitForXmlSnippet(a_xml, "A.java");
            var fileUnitB = fileSetup.GetFileUnitForXmlSnippet(b_xml, "B.java");

            var scopeForA = codeParser.ParseFileUnit(fileUnitA);
            var scopeForB = codeParser.ParseFileUnit(fileUnitB);
            var globalScope = scopeForA.Merge(scopeForB);

            var aDotContains = globalScope.ChildScopes.First().ChildScopes.First() as MethodDefinition;
            var bDotContains = globalScope.ChildScopes.Last().ChildScopes.First() as MethodDefinition;

            Assert.AreEqual("A.Contains", aDotContains.FullName);
            Assert.AreEqual("B.Contains", bDotContains.FullName);

            Assert.AreSame(bDotContains, aDotContains.MethodCalls.First().FindMatches().First());
            Assert.AreNotSame(aDotContains, aDotContains.MethodCalls.First().FindMatches().First());
        }
    }
}
