//---------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Description: Implementation of a FriendAccessAllowedAttribute attribute that is used to mark internal metadata
//              that is allowed to be accessed from friend assemblies.
//
// History:
//  2-28-2005 mleonov - created it.
//
//------------------------------------------------------------------------


using System;

#if WINDOWS_BASE
namespace MS.Internal.WindowsBase
#elif PRESENTATION_CORE
namespace MS.Internal.PresentationCore 
#elif PRESENTATIONFRAMEWORK
namespace MS.Internal.PresentationFramework
#elif PRESENTATION_CFF_RASTERIZER
namespace MS.Internal.PresentationCffRasterizer
#elif PRESENTATIONUI
namespace MS.Internal.PresentationUI
#elif DRT
namespace MS.Internal.Drt
#elif SYSTEM_XAML
namespace MS.Internal.WindowsBase  //current copy of XmlMarkupCompatibilityReader uses this ns for FAAA.
#else
#error Attempt to define FriendAccessAllowedAttribute in an unknown assembly.
namespace MS.Internal.YourAssemblyName
#endif
{
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Method |
        AttributeTargets.Struct |
        AttributeTargets.Enum |
        AttributeTargets.Interface |
        AttributeTargets.Delegate |
        AttributeTargets.Constructor,
        AllowMultiple = false,
        Inherited = true)
    ]
    internal sealed class FriendAccessAllowedAttribute : Attribute
    {
    }
}

