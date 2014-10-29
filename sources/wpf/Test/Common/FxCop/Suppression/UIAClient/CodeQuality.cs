using System.Diagnostics.CodeAnalysis;

// Using Avalon's pinvokes
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MS.Win32.UnsafeNativeMethods.IntWindowFromPoint(MS.Win32.UnsafeNativeMethods+POINTSTRUCT):System.IntPtr")]

// Code copied from WindowBase that we don't own (these are for LH files only)
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Internal.TokenizerHelper.NextToken():System.Boolean")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Internal.TokenizerHelper.NextTokenRequired(System.Boolean,System.Char):System.String")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Internal.TokenizerHelper.NextTokenRequired(System.Boolean):System.String")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Internal.TokenizerHelper.NextAndLastTokenRequired(System.Boolean):System.String")]
// Code generated by gensr.pl
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "System.Windows.STID..ctor(System.String)")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "System.Windows.STID.get_Default():System.Windows.STID")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "System.Windows.SRID.get_Default():System.Windows.SRID")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Win32.NativeMethods+RECT.get_IsEmpty():System.Boolean")]
// We don't own these names; the OS does
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="hwnd", Scope="member", Target="System.Windows.Automation.AutomationElement.#FromHandle(System.IntPtr)")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.TransformPattern.Move(System.Double,System.Double):System.Void", MessageId = "0#x")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.TransformPattern.Move(System.Double,System.Double):System.Void", MessageId = "1#y")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="Impl", Scope="member", Target="System.Windows.Automation.AutomationElement.#FromLocalProvider(System.Windows.Automation.Provider.IRawElementProviderSimple)")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="hwnd", Scope="type", Target="System.Windows.Automation.ClientSideProviderFactoryCallback")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="y",    Scope="member", Target="System.Windows.Automation.TransformPattern.#Move(System.Double,System.Double)")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="x",    Scope="member", Target="System.Windows.Automation.TransformPattern.#Move(System.Double,System.Double)")]
[module: SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Scope = "member", Target = "System.Windows.Input.Key.LineFeed")]
// No way to Marshal an array of SafeHandles to an array of IntPtrs for MsgWaitForMultipleObjects
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.AutomationProxies.Misc.MsgWaitForMultipleObjects(Microsoft.Win32.SafeHandles.SafeWaitHandle,System.Boolean,System.Int32,System.Int32):System.Int32", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.Misc.TryMsgWaitForMultipleObjects(Microsoft.Win32.SafeHandles.SafeWaitHandle,System.Boolean,System.Int32,System.Int32,System.Int32&):System.Int32", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
// Exceptions are filtered and rethrown per suggestion on devdiv site


// The type that is passed is correct
[module: SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Scope = "member", Target = "System.Windows.Automation.Provider.AutomationInteropProvider.RaiseAutomationEvent(System.Windows.Automation.AutomationEvent,System.Windows.Automation.Provider.IRawElementProviderSimple,System.Windows.Automation.AutomationEventArgs):System.Void")]
[module: SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Scope = "member", Target = "System.Windows.Automation.Automation.PropertyName(System.Windows.Automation.AutomationProperty):System.String")]
[module: SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Scope = "member", Target = "System.Windows.Automation.Automation.PatternName(System.Windows.Automation.AutomationPattern):System.String")]
[module: SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Scope = "member", Target = "System.Windows.Automation.ItemContainerPattern.#FindItemByProperty(System.Windows.Automation.AutomationElement,System.Windows.Automation.AutomationProperty,System.Object)")]
[module: SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Scope="type", Target="System.Windows.Automation.AutomationFocusChangedEventArgs")]

// This Marshalling of these types are proper considering the unKnown Target platform which may not be WPF itself as well.
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope="member", Target="MS.Win32.UnsafeNativeMethods.#SendMessageTimeout(MS.Win32.NativeMethods+HWND,System.Int32,System.IntPtr,System.Text.StringBuilder,System.Int32,System.Int32,System.IntPtr&)", MessageId="3")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope="member", Target="MS.Win32.SafeNativeMethods.#RealGetWindowClass(MS.Win32.NativeMethods+HWND,System.Text.StringBuilder,System.Int32)", MessageId="1")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope="member", Target="MS.Win32.SafeNativeMethods.#GetClassName(MS.Win32.NativeMethods+HWND,System.Text.StringBuilder,System.Int32)", MessageId="1")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope="member", Target="MS.Win32.SafeNativeMethods.#GlobalAddAtom(System.String)", MessageId="0")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope="member", Target="MS.Win32.SafeNativeMethods.#GetModuleFileNameEx(MS.Internal.Automation.SafeProcessHandle,System.IntPtr,System.Text.StringBuilder,System.Int32)", MessageId="2")]

// This doesn't need Globalization
[module: SuppressMessage("Microsoft.Globalization", "CA1307:SpecifyStringComparison", Scope="member", Target="MS.Internal.Automation.HwndProxyElementProvider.#IsProgmanWindow(MS.Win32.NativeMethods+HWND)", MessageId="System.String.CompareTo(System.String)")]

// Reviewed by atgarch and these enums are OK
[module: SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames", Scope = "type", Target = "System.Windows.Automation.TreeScope")]
[module: SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames", Scope = "type", Target = "System.Windows.Automation.ClientSideProviderMatchIndicator")]
[module: SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames", Scope = "type", Target = "System.Windows.Automation.SupportedTextSelection")]
// Reviewed and these are spelled correctly
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.AutomationElement.IsOffscreenProperty")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.AutomationElement+AutomationElementInformation.IsOffscreen")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.TextPattern.OverlineStyleAttribute")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.TextPattern.OverlineColorAttribute")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.Provider.AutomationInteropProvider.ReturnRawElementProvider(System.IntPtr,System.IntPtr,System.IntPtr,System.Windows.Automation.Provider.IRawElementProviderSimple):System.IntPtr")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.Provider.IWindowProvider.Maximizable")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.Provider.IWindowProvider.Minimizable")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Rect.Offset(System.Windows.Rect,System.Double,System.Double):System.Windows.Rect")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Rect.Offset(System.Double,System.Double):System.Void")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Point.Offset(System.Double,System.Double):System.Void")]
[module: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "System.Windows.Automation.Text.CapStyle.Unicase")]

// Reviewed and these are correctly cased
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "ExceptionStringTable.resources", MessageId = "classname")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "ExceptionStringTable.resources", MessageId = "imagename")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "ExceptionStringTable.resources", MessageId = "nonenabled")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "ExceptionStringTable.resources", MessageId = "clientside")]
[module: SuppressMessage("Microsoft.Naming","CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId="hwnd", Scope="resource", Target="ExceptionStringTable.resources")]
[module: SuppressMessage("Microsoft.Naming","CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId="nonclient", Scope="resource", Target="ExceptionStringTable.resources")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "StringTable.resources", MessageId = "dataitem")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "StringTable.resources", MessageId = "datagrid")]

// Reviewed and these are correctly suffixed
[module: SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Scope = "type", Target = "System.Windows.Automation.AutomationTextAttribute")]
[module: SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Scope = "type", Target = "System.Windows.Automation.PropertyConditionFlags")]
// These could just be moved outside of the using class but having the class nested helps code readability
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.MultipleViewPattern+MultipleViewPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.GridPattern+GridPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.TogglePattern+TogglePatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.SelectionPattern+SelectionPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.RangeValuePattern+RangeValuePatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.TablePattern+TablePatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.ValuePattern+ValuePatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.SelectionItemPattern+SelectionItemPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.AutomationElement+AutomationElementInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.GridItemPattern+GridItemPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.ExpandCollapsePattern+ExpandCollapsePatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.WindowPattern+WindowPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.DockPattern+DockPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.TableItemPattern+TableItemPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.ScrollPattern+ScrollPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope = "type", Target = "System.Windows.Automation.TransformPattern+TransformPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Scope="type", Target="System.Windows.Automation.SynchronizedInputPattern+SynchronizedInputPatternInformation")]

// These classes don't typically get compared
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.MultipleViewPattern+MultipleViewPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.GridPattern+GridPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.TogglePattern+TogglePatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.SelectionPattern+SelectionPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.RangeValuePattern+RangeValuePatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.TablePattern+TablePatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.ValuePattern+ValuePatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.SelectionItemPattern+SelectionItemPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.AutomationElement+AutomationElementInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.GridItemPattern+GridItemPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.ExpandCollapsePattern+ExpandCollapsePatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.WindowPattern+WindowPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.DockPattern+DockPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.TableItemPattern+TableItemPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.ScrollPattern+ScrollPatternInformation")]
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.TransformPattern+TransformPatternInformation")]
[module: SuppressMessage("Microsoft.Performance","CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope="type", Target="System.Windows.Automation.SynchronizedInputPattern+SynchronizedInputPatternInformation")]
[module: SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes", Scope = "type", Target = "System.Windows.Automation.AutomationIdentifier")]
// Not exposed for V1; Consider this rule for V2
[module: SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Scope = "type", Target = "System.Windows.Automation.ClientSideProviderDescription")]
// False positives
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope="member", Target="System.Windows.Automation.AutomationElement.Find(System.Windows.Automation.TreeScope,System.Windows.Automation.Condition,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest,System.Boolean,System.ComponentModel.BackgroundWorker):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse[]")]
// Not required for non-public code
[module: SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Scope = "type", Target = "System.Windows.Automation.Automation")]
// SafeHandle isn't appropriate here; these are wrapped in classes that release resources though
[module: SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources", Scope = "member", Target = "MS.Internal.Automation.Accessible._hwnd")]

//don't need assembly-level critical on this assembly
[module: SuppressMessage("Microsoft.SecurityCritical", "Test002:SecurityCriticalMembersMustExistInCriticalTypesAndAssemblies")]

// Always returns S_OK
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId="MS.Internal.Automation.UiaCoreApi.SetErrorInfo(System.Int32,System.IntPtr)", Scope="member", Target="MS.Internal.Automation.UiaCoreApi.#IsErrorMarker(System.Object,System.Boolean)")]

// Related to Usage
[module: SuppressMessage("Microsoft.Usage","CA1816:CallGCSuppressFinalizeCorrectly", Scope="member", Target="System.Windows.Automation.CacheRequestActivation.#Dispose()")]


//Related to ST and SR class:
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.SR.Get(System.String,System.Object[]):System.String")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.SR.get_ResourceManager():System.Resources.ResourceManager")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.ST.Get(System.String,System.Object[]):System.String")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.ST.Get(System.Windows.STID,System.Object[]):System.String")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.ST.get_ResourceManager():System.Resources.ResourceManager")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.STID.get_String():System.String")]
[module: SuppressMessage("Microsoft.Performance","CA1811:AvoidUncalledPrivateCode", Scope="member", Target="System.Windows.ST.#Get(System.Windows.STID)")]


// SafeHandle is not marshalled when in Structs.  See comments in code.
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNodeFromFocus(MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNodeFromPoint(System.Double,System.Double,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNavigate(MS.Internal.Automation.SafeNodeHandle,System.Windows.Automation.Provider.NavigateDirection,System.Windows.Automation.Condition,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaAddEvent(MS.Internal.Automation.SafeNodeHandle,System.Int32,MS.Internal.Automation.UiaCoreApi+UiaEventCallback,System.Windows.Automation.TreeScope,System.Int32[],MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.SafeEventHandle", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaFind(MS.Internal.Automation.SafeNodeHandle,MS.Internal.Automation.UiaCoreApi+UiaFindParams,System.Windows.Automation.Condition,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse[]", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaGetUpdatedCache(MS.Internal.Automation.SafeNodeHandle,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest,MS.Internal.Automation.UiaCoreApi+NormalizeState,System.Windows.Automation.Condition):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "System.Windows.Automation.NotCondition..ctor(System.Windows.Automation.Condition)", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "System.Windows.Automation.AndCondition..ctor(System.Windows.Automation.Condition[])", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "System.Windows.Automation.OrCondition..ctor(System.Windows.Automation.Condition[])", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
[module: SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Scope = "member", Target = "MS.Internal.AutomationProxies.Accessible.GetFullAccessibleChild(System.Int32):MS.Internal.AutomationProxies.Accessible")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNodeFromFocus(MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNodeFromPoint(System.Double,System.Double,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaNavigate(MS.Internal.Automation.SafeNodeHandle,System.Windows.Automation.Provider.NavigateDirection,System.Windows.Automation.Condition,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaAddEvent(MS.Internal.Automation.SafeNodeHandle,System.Int32,MS.Internal.Automation.UiaCoreApi+UiaEventCallback,System.Windows.Automation.TreeScope,System.Int32[],MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.SafeEventHandle")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaFind(MS.Internal.Automation.SafeNodeHandle,MS.Internal.Automation.UiaCoreApi+UiaFindParams,System.Windows.Automation.Condition,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse[]")]
[module: SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive", Scope = "member", Target = "MS.Internal.Automation.UiaCoreApi.UiaGetUpdatedCache(MS.Internal.Automation.SafeNodeHandle,MS.Internal.Automation.UiaCoreApi+UiaCacheRequest,MS.Internal.Automation.UiaCoreApi+NormalizeState,System.Windows.Automation.Condition):MS.Internal.Automation.UiaCoreApi+UiaCacheResponse")]
