﻿//----------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------

namespace System.Activities.Presentation.FreeFormEditing
{
    using System.Windows;

    /// <summary>
    /// Defines the interface for the container that enables auto-split
    /// </summary>
    internal interface IAutoSplitContainer
    {
        bool CanAutoSplit(DragEventArgs e);

        void DoAutoSplit(DragEventArgs e, Connector connector);
    }
}
