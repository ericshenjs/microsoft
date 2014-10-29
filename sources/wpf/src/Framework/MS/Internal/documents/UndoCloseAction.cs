//---------------------------------------------------------------------------
//
// File: UndoCloseAction.cs
//
// Description: Enum for UndoManager.Close method.
//
//---------------------------------------------------------------------------

using System;

namespace MS.Internal.Documents
{
    /// <summary>
    /// Specifies the type of change applied to TextContainer content.
    /// </summary>
    internal enum UndoCloseAction
    { 
        /// <summary>
        /// Keep the open undo unit.
        /// </summary>
        Commit,

        /// <summary>
        /// Rollback the undo undo.  This calls unit.Do to undo the changes.
        /// </summary>
        Rollback, 

        /// <summary>
        /// Throw away the undo unit without calling unit.Do.
        /// BE CAREFUL!  If the unit contains any changes that modified the
        /// state of the underlying content, the undo stack may be corrupt.
        /// </summary>
        Discard,
    }
}
