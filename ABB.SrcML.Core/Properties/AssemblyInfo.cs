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

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Resources;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("ABB.SrcML.Core")]
[assembly: AssemblyProduct("ABB.SrcML.Core")]
[assembly: AssemblyDescription("Core functionality for ABB SrcML")]
[assembly: AssemblyCopyright("Copyright © ABB 2013")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: CLSCompliant(true)]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("ec7a442d-da4b-459e-8213-6babd953f2af")]

[assembly: AssemblyCompany("ABB")]
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: AssemblyVersion("2.0.0.0")]
