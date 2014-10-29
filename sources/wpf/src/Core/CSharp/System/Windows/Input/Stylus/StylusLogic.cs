using System;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using MS.Win32; // for *NativeMethods
using Microsoft.Win32; // for RegistryKey class
using System.Runtime.InteropServices;
using System.Security;
using MS.Internal;
using MS.Internal.Interop;
using MS.Utility;
using System.Windows.Input.StylusPlugIns;
using System.Security.Permissions;
using System.Collections.Generic;
using SR=MS.Internal.PresentationCore.SR;
using SRID=MS.Internal.PresentationCore.SRID;
using MS.Internal.PresentationCore;                        // SecurityHelper


namespace System.Windows.Input
{
    /////////////////////////////////////////////////////////////////////////

    internal class StylusLogic: DispatcherObject
    {
        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: This code accesses critical data InputManager and
        ///              SecurityCritical method  UnsecureCurrent.
        ///              It also returns StylusLogic which is not safe to return
        /// </SecurityNote>
        static internal StylusLogic CurrentStylusLogic
        {
            [SecurityCritical]
            get
            {
                return InputManager.UnsecureCurrent.StylusLogic;
            }
        }


        /////////////////////////////////////////////////////////////////////

        ///<SecurityNote>
        /// Critical - Calls a critical method - PreProcessInput
        ///          - Accesses critical data _inputManager.Value
        ///</SecurityNote>
        [SecurityCritical]
        internal StylusLogic(InputManager inputManager)
        {
            _inputManager = new SecurityCriticalData<InputManager>(inputManager);;
            _inputManager.Value.PreProcessInput   += new PreProcessInputEventHandler(PreProcessInput);
            _inputManager.Value.PreNotifyInput    += new NotifyInputEventHandler(PreNotifyInput);
            _inputManager.Value.PostProcessInput  += new ProcessInputEventHandler(PostProcessInput);

#if !MULTICAPTURE
            _overIsEnabledChangedEventHandler = new DependencyPropertyChangedEventHandler(OnOverIsEnabledChanged);
            _overIsVisibleChangedEventHandler = new DependencyPropertyChangedEventHandler(OnOverIsVisibleChanged);
            _overIsHitTestVisibleChangedEventHandler = new DependencyPropertyChangedEventHandler(OnOverIsHitTestVisibleChanged);
            _reevaluateStylusOverDelegate = new DispatcherOperationCallback(ReevaluateStylusOverAsync);
            _reevaluateStylusOverOperation = null;

            _captureIsEnabledChangedEventHandler = new DependencyPropertyChangedEventHandler(OnCaptureIsEnabledChanged);
            _captureIsVisibleChangedEventHandler = new DependencyPropertyChangedEventHandler(OnCaptureIsVisibleChanged);
            _captureIsHitTestVisibleChangedEventHandler = new DependencyPropertyChangedEventHandler(OnCaptureIsHitTestVisibleChanged);
            _reevaluateCaptureDelegate = new DispatcherOperationCallback(ReevaluateCaptureAsync);
            _reevaluateCaptureOperation = null;
#endif


            _shutdownHandler = new EventHandler(this.OnDispatcherShutdown);
            _processDisplayChanged = new DispatcherOperationCallback(ProcessDisplayChanged);
            _processDeferredMouseMove = new DispatcherOperationCallback(ProcessDeferredMouseMove);

            ReadSystemConfig();

            _dlgInputManagerProcessInput = new DispatcherOperationCallback(InputManagerProcessInput);
        }

        /// <SecurityNote>
        ///     Critical: - Call critical method: TabletDeviceCollection.DisposeTablets().
        ///               - Accesses SecurityCriticalData __penContextsMap and _InputManager.Value.
        /// </SecurityNote>
        [SecurityCritical]
        void OnDispatcherShutdown(object sender, EventArgs e)
        {
            if(_shutdownHandler != null)
                _inputManager.Value.Dispatcher.ShutdownFinished -= _shutdownHandler;

            if (_tabletDeviceCollection != null)
            {
                // Clean up our state when the dispatcher exits.  If a new dispatcher
                // happens to be created on this thread again we'll create everything fresh.
                _tabletDeviceCollection.DisposeTablets();
                _tabletDeviceCollection = null;
                _tabletDeviceCollectionDisposed = true;
            }
            _currentStylusDevice = null; // no active stylus device any more.
            // NOTE: __penContextsMap will be cleaned up by HwndSource Dispose() so we don't worry about that.
        }


        /// <summary>
        /// Grab the defualts from the registry for the tap and mouse fequency/resolution
        /// </summary>
        ///<SecurityNote>
        /// Critical - Asserts read registry permission...
        ///           - TreatAsSafe boundry is the constructor
        ///           - called by constructor
        ///</SecurityNote>
        [SecurityCritical]
        private void ReadSystemConfig()
        {
            object obj;
            RegistryKey key = null; // This object has finalizer to close the key.

            // Acquire permissions to read the one key we care about from the registry
            new RegistryPermission(RegistryPermissionAccess.Read,
                "HKEY_CURRENT_USER\\Software\\Microsoft\\Wisp\\Pen\\SysEventParameters").Assert(); // BlessedAssert
            try
            {
                key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Wisp\\Pen\\SysEventParameters");

                if ( key != null )
                {
                    obj = key.GetValue("DblDist");
                    _doubleTapDelta = (obj == null) ? _doubleTapDelta : (Int32)obj;      // The default double tap distance is 15 pixels (value is given in pixels)

                    obj = key.GetValue("DblTime");
                    _doubleTapDeltaTime = (obj == null) ? _doubleTapDeltaTime : (Int32)obj;      // The default double tap timeout is 800ms

                    obj = key.GetValue("Cancel");
                    _cancelDelta = (obj == null) ? _cancelDelta : (Int32)obj;      // The default move delta is 40 (4mm)
                }
            }
            finally
            {
                RegistryPermission.RevertAssert();
            }

            if ( key != null )
            {
                key.Close();
            }
        }


        /////////////////////////////////////////////////////////////////////
        /// <SecurityNote>
        /// Critical - calls security critical code (ProcessInputReport)
        /// </SecurityNote>
        [SecurityCritical]
        internal void ProcessSystemEvent(PenContext penContext,
                                                  int tabletDeviceId,
                                                  int stylusDeviceId,
                                                  int timestamp,
                                                  SystemGesture systemGesture,
                                                  int gestureX,
                                                  int gestureY,
                                                  int buttonState,
                                                  PresentationSource inputSource)
        {
            // We only want to process the system events we expose in the public enum
            // for SystemSystemGesture.  There are a bunch of other system gestures that
            // can come through.
            if (systemGesture == SystemGesture.Tap ||
                systemGesture == SystemGesture.RightTap ||
                systemGesture == SystemGesture.Drag ||
                systemGesture == SystemGesture.RightDrag ||
                systemGesture == SystemGesture.HoldEnter ||
                systemGesture == SystemGesture.HoldLeave ||
                systemGesture == SystemGesture.HoverEnter ||
                systemGesture == SystemGesture.HoverLeave ||
                systemGesture == SystemGesture.Flick ||
                systemGesture == RawStylusSystemGestureInputReport.InternalSystemGestureDoubleTap ||
                systemGesture == SystemGesture.None)
            {
                Debug.Assert(systemGesture != SystemGesture.None);  // We should ever see this as input.
                RawStylusSystemGestureInputReport inputReport =
                            new RawStylusSystemGestureInputReport(
                                   InputMode.Foreground,
                                   timestamp,
                                   inputSource,
                                   penContext,
                                   tabletDeviceId,
                                   stylusDeviceId,
                                   systemGesture,
                                   gestureX, // location of system gesture in tablet device coordinates
                                   gestureY,
                                   buttonState); // flicks passes the flickinfo in this param

                // actions: RawStylusActions.StylusSystemEvent
                ProcessInputReport(inputReport);
            }
        }

        /////////////////////////////////////////////////////////////////////
        // on pen/RTI thread

        /// <SecurityNote>
        /// Critical - calls security critical code (ProcessInputReport)
        /// </SecurityNote>
        [SecurityCritical]
        internal void ProcessInput(
                            RawStylusActions actions,
                            PenContext penContext,
                            int tabletDeviceId,
                            int stylusDeviceId,
                            int[] data,
                            int timestamp,
                            PresentationSource inputSource)
        {
            RawStylusInputReport inputReport =
                new RawStylusInputReport(InputMode.Foreground,
                                         timestamp,
                                         inputSource,
                                         penContext,
                                         actions,
                                         tabletDeviceId,
                                         stylusDeviceId,
                                         data);

            ProcessInputReport(inputReport);
        }

        /////////////////////////////////////////////////////////////////////
        // NOTE: this is invoked on the pen thread, outside of Dispatcher

        /////////////////////////////////////////////////////////////////////
        /// <SecurityNote>
        /// Critical - Calls security critical routine (InvokeStylusPluginCollection)
        ///           - Access SecurityCritical data (_queueStylusEvents, RawStylusInputReport.PenContexts).
        ///           - PenThreadWorker.ThreadProc() is TAS boundry
        /// </SecurityNote>
        [SecurityCritical]
        void ProcessInputReport(RawStylusInputReport inputReport)
        {
            // First, assign the StylusDevice (note it may still be null for new StylusDevice)
            inputReport.StylusDevice = FindStylusDeviceWithLock(inputReport.StylusDeviceId);

            // Only call plugins if we are not in a drag drop operation and the HWND is enabled!
            if (!_inDragDrop || !inputReport.PenContext.Contexts.IsWindowDisabled)
            {
                // Handle real time input (call StylusPlugIns)
                InvokeStylusPluginCollection(inputReport);
            }

            // ETW event indicating that a stylus input report was queued.
            EventTrace.EasyTraceEvent(EventTrace.Keyword.KeywordInput | EventTrace.Keyword.KeywordPerf, EventTrace.Level.Info, EventTrace.Event.StylusEventQueued, inputReport.StylusDeviceId);

            // Queue up new event.
            lock(_stylusEventQueueLock)
            {
                _queueStylusEvents.Enqueue(inputReport);
            }

            // post the args into dispatcher queue
            Dispatcher.BeginInvoke(DispatcherPriority.Input, _dlgInputManagerProcessInput, null);
        }

        /////////////////////////////////////////////////////////////////////
        // this is invoked from within the Dispatcher the _inputManager is affiliated to

        /// <SecurityNote>
        /// Critical - uses security critical data (_inputManager)
        ///               calls security critical code ProcessInput which has a link demand (UIPermissionAttribute)
        ///             TAS boundry at Synchronize and StylusLogic constructor
        /// </SecurityNote>
        [SecurityCritical]
        internal object InputManagerProcessInput(object oInput)
        {
            RawStylusInputReport rawStylusInputReport = null;

            // Now grab the queued up Stylus input reports and process them.
            lock(_stylusEventQueueLock)
            {
                if (_queueStylusEvents.Count > 0)
                {
                    rawStylusInputReport = _queueStylusEvents.Dequeue();
                }
            }

            if (rawStylusInputReport != null)
            {
                PenContext penContext = rawStylusInputReport.PenContext;
                if (penContext.UpdateScreenMeasurementsPending &&
                    rawStylusInputReport.StylusDevice != null)
                {
                    TabletDevice tabletDevice = rawStylusInputReport.StylusDevice.TabletDevice;
                    bool areSizeDeltasValid = tabletDevice.AreSizeDeltasValid();

                    // Update screen measurements
                    penContext.UpdateScreenMeasurementsPending = false;
                    tabletDevice.UpdateScreenMeasurements();

                    if (areSizeDeltasValid)
                    {
                        // Update TabletDevice.DoubleTapDelta and TabletDevice.CancelDelta if needed.
                        tabletDevice.UpdateSizeDeltas(penContext.StylusPointDescription, this);
                    }
                }

                // build InputReportEventArgs
                InputReportEventArgs input = new InputReportEventArgs(null, rawStylusInputReport);
                input.RoutedEvent=InputManager.PreviewInputReportEvent;
                // Set flag to prevent reentrancy due to wisptis mouse event getting triggered
                // while processing this stylus event.
                _processingQueuedEvent = true;
                try
                {
                    InputManagerProcessInputEventArgs(input);
                }
                finally
                {
                    _processingQueuedEvent = false;
                }
            }

            return null;
        }

        /////////////////////////////////////////////////////////////////////
        /// <SecurityNote>
        /// Critical - calls security critical code (ProcessInput)
        ///           - TAS boundry at StylusDevice::ChangeStylusCapture
        /// </SecurityNote>
        [SecurityCritical]
        internal void InputManagerProcessInputEventArgs(InputEventArgs input)
        {
            _inputManager.Value.ProcessInput(input);
        }


        /// <SecurityNote>
        /// Critical - Accesses security critical data _deferredMouseMove.
        /// </SecurityNote>
        [SecurityCritical]
        private bool DeferMouseMove(RawMouseInputReport mouseInputReport)
        {
            if (!_triedDeferringMouseMove)
            {
                if (_deferredMouseMove != null)
                {
                    return false; // only allow one at a time.
                }
                else
                {
                    _deferredMouseMove = mouseInputReport;

                    // Now make the deferred call to the process the mouse move.
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, _processDeferredMouseMove, null);
                }
                return true;
            }
            return false;
        }

        /// <SecurityNote>
        /// Critical - Accesses security critical data _deferredMouseMove.
        ///
        ///          - Calls security critical method SendDeferredMouseEvent.
        ///
        /// TreatAsSafe - Just causes us to resend a mouse move event that we have
        ///                 deferred.  Once sent the mouse event ref is cleared so only single
        ///                 mouse event can ever be sent.  No data goes in or out.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        internal object ProcessDeferredMouseMove(object oInput)
        {
            // Make sure we haven't flushed the deferred event before dispatcher version processes.
            if (_deferredMouseMove != null)
            {
#if !MULTICAPTURE
                // See if a stylus is now in range.
                if ((CurrentStylusDevice == null || !CurrentStylusDevice.InRange))
                {
                    SendDeferredMouseEvent(true);
                }
                else
                {
                    // We are now inRange so eat this.
                    SendDeferredMouseEvent(false);
                }
#else
                // See if a stylus is now in range and eat messages
                // when a stylus is in range.
                SendDeferredMouseEvent(!_stylusDeviceInRange);
#endif
            }
            return null;
        }

        /// <SecurityNote>
        /// Critical - Accesses security critical data _deferredMouseMove and _inputManager.Value.
        ///
        ///          - Calls security critical methods InputReportEventArgs
        ///                 constructor, InputManager.ProcessInput.
        ///
        /// </SecurityNote>
        [SecurityCritical]
        private void SendDeferredMouseEvent(bool sendInput)
        {
            if (sendInput)
            {
                _triedDeferringMouseMove = true;  // Only reset to not try again if we don't find we are in range.

                // Only send if we have valid PresentationSource and CompositionTarget.
                if (_deferredMouseMove != null && _deferredMouseMove.InputSource != null &&
                    _deferredMouseMove.InputSource.CompositionTarget != null &&
                    !_deferredMouseMove.InputSource.CompositionTarget.IsDisposed)
                {
                    // Process mouse move now since nothing else from stylus came through...
                    InputReportEventArgs mouseArgs = new InputReportEventArgs(_inputManager.Value.PrimaryMouseDevice, _deferredMouseMove);
                    mouseArgs.RoutedEvent = InputManager.PreviewInputReportEvent;
                    _deferredMouseMove = null; // Clear this out before sending.
                    // This will cause _lastMoveFromStylus to be set to false.
                    _inputManager.Value.ProcessInput(mouseArgs);
                }
            }

            // We no longer need the ref on the cached input report.
            _deferredMouseMove = null;
        }


        ///<SecurityNote>
        /// Critical: calls a critical function - UpdateTarget.
        ///           accesses e.StagingItem.Input and InputReport.InputSource and _inputManager.Value.
        ///            It can also be used for Input spoofing.
        ///</SecurityNote>
        [SecurityCritical]
        private void PreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (_inputEnabled)
            {
                if(e.StagingItem.Input.RoutedEvent == InputManager.PreviewInputReportEvent)
                {
                    InputReportEventArgs input = e.StagingItem.Input as InputReportEventArgs;

                    if(input != null && !input.Handled)
                    {
                        // See if we are in a DragDrop operation.  If so set our internal flag
                        // which stops us from promoting Stylus or Mouse events!
                        if (_inDragDrop != _inputManager.Value.InDragDrop)
                        {
                            _inDragDrop = _inputManager.Value.InDragDrop;

                            // If we are going out of DragDrop then we need to re [....] the mouse state
                            // if we have a stylus device in range (otherwise we [....] on the next
                            // stylus coming in range).
                            if (!_inDragDrop && _stylusDeviceInRange)
                            {
                                UpdateMouseState();
                            }
                        }

                        if (input.Report.Type == InputType.Mouse)
                        {
                            // If we see a non stylus mouse event (not triggered from stylus event)
                            if ((input.Device as StylusDevice) == null)
                            {
                                // And we only do work if we are enabled for stylus input (ie - have tablet devices)
                                // and the tablet device collection hasnt been disposed yet.
                                if (!_tabletDeviceCollectionDisposed && TabletDevices.Count != 0)
                                {
                                    RawMouseInputReport mouseInputReport = (RawMouseInputReport) input.Report;
                                    RawMouseActions actions = mouseInputReport.Actions;
                                    int mouseExtraInfo = NativeMethods.IntPtrToInt32(mouseInputReport.ExtraInformation);
                                    bool fromWisptis = (mouseExtraInfo & 0xFFFFFF00) == 0xFF515700; // MI_WP_SIGNATURE

                                    // Grab the stylus info if from wisptis
                                    if (fromWisptis)
                                    {
                                        _lastMouseMoveFromStylus = true;

                                        // Grab the current stylus Id out of the extra info.
                                        _lastStylusDeviceId = (mouseExtraInfo & 0x000000FF);
                                    }

                                    // If mouse is getting deactivated and StylusOver is non null then force stylusover to null.
                                    if ((actions & RawMouseActions.Deactivate) == RawMouseActions.Deactivate)
                                    {
                                        _seenRealMouseActivate = false;

                                        if (CurrentStylusDevice != null)
                                        {
                                            PenContexts penContexts = GetPenContextsFromHwnd(mouseInputReport.InputSource);

                                            // If we are inRange still then defer the Deactivate call till we are OutOfRange.
                                            if (_stylusDeviceInRange && !_inDragDrop && (penContexts == null || !penContexts.IsWindowDisabled))
                                            {
                                                _mouseDeactivateInputReport = mouseInputReport;
                                                e.Cancel();
                                                input.Handled = true;
                                                return;
                                            }
#if !MULTICAPTURE
                                            else if (CurrentStylusDevice.DirectlyOver != null)
#else
                                            else
#endif
                                            {
                                                MouseDevice mouseDevice = _inputManager.Value.PrimaryMouseDevice;

                                                if (mouseDevice.CriticalActiveSource == mouseInputReport.InputSource)
                                                {
#if !MULTICAPTURE
                                                    // Update over to be null when deactivating.
                                                    CurrentStylusDevice.ChangeStylusOver(null);
#else
                                                    lock (__stylusDeviceLock)
                                                    {
                                                        foreach (var pair in __stylusDeviceMap)
                                                        {
                                                            var currentDevice = pair.Value;
                                                            if (currentDevice.DirectlyOver != null)
                                                            {
                                                                // Update over to be null when deactivating.
                                                                currentDevice.ChangeStylusOver(null);
                                                            }
                                                        }
                                                    }
#endif
                                                }
                                            }
                                        }
                                    }
                                    // See if we got some mouse input we need to check for consistency (not tagged from wisptis)
                                    else if ((actions & RawMouseActions.CancelCapture) != 0)
                                    {
                                        // We need to resend this back through as coming from a stylusdevice if in range
                                        if (CurrentStylusDevice != null && CurrentStylusDevice.InRange)
                                        {
                                            RawMouseInputReport cancelCaptureInputReport =
                                                        new RawMouseInputReport(mouseInputReport.Mode,
                                                                                mouseInputReport.Timestamp,
                                                                                mouseInputReport.InputSource,
                                                                                mouseInputReport.Actions,
                                                                                0, // Rest of the parameters are not used...
                                                                                0,
                                                                                0,
                                                                                IntPtr.Zero);

                                            InputReportEventArgs args = new InputReportEventArgs(CurrentStylusDevice, cancelCaptureInputReport);
                                            args.RoutedEvent = InputManager.PreviewInputReportEvent;
                                            e.Cancel();
                                            _inputManager.Value.ProcessInput(args);
                                        }
                                    }
                                    // Handle the Mouse activation
                                    else if ((actions & RawMouseActions.Activate) != 0)
                                    {
                                        // If mouse is getting Activated and we ate a Deactivate then clear the cached Deactivate.
                                        _mouseDeactivateInputReport = null;

                                        // We process Activate events and make sure to clear any other actions if we are resending
                                        // this from a StylusDevice.  This is so we don't get a move generated before we see the
                                        // StylusDevice InRange event and the following StylusMove which will generate a MouseMove.
                                        StylusDevice activateStylusDevice = null;
                                        _seenRealMouseActivate = true;

                                        // See if we need to process this event from us.
                                        if (CurrentStylusDevice != null && CurrentStylusDevice.InRange)
                                            activateStylusDevice = CurrentStylusDevice;
                                        else if (fromWisptis || ShouldConsiderStylusInRange(mouseInputReport))
                                            activateStylusDevice = FindStylusDevice(_lastStylusDeviceId);

                                        // We need to resend this as coming from a stylusdevice if in range possibly.
                                        if (activateStylusDevice != null)
                                        {
                                            // Check to se if we have already Activated the mouse from a stylus event.
                                            // If not then we need to let this one go through marked from us if we are in range!
                                            if (mouseInputReport.InputSource != _inputManager.Value.PrimaryMouseDevice.CriticalActiveSource)
                                            {
                                                Point pt;

                                                pt = activateStylusDevice.LastMouseScreenPoint; // Use last promoted mouse location.
                                                pt = PointUtil.ScreenToClient(pt, mouseInputReport.InputSource);

                                                RawMouseInputReport activateInputReport =
                                                            new RawMouseInputReport(mouseInputReport.Mode,
                                                                                    mouseInputReport.Timestamp,
                                                                                    mouseInputReport.InputSource,
                                                                                    RawMouseActions.Activate, // Only let activate happen.
                                                                                    (int)pt.X,
                                                                                    (int)pt.Y,
                                                                                    mouseInputReport.Wheel,
                                                                                    mouseInputReport.ExtraInformation);

                                                InputReportEventArgs args = new InputReportEventArgs(activateStylusDevice, activateInputReport);
                                                args.RoutedEvent = InputManager.PreviewInputReportEvent;
                                                _inputManager.Value.ProcessInput(args);
                                            }

                                            // If stylus is active then eat this since we'll send the activate.  We just cancel
                                            // to ensure the mouse event from HwndMouseInputProvider returns that it was not handled.
                                            // The mouse device code will not do anything with the event during PreProcessInput and
                                            // it will not see a PreNotifyInput event for this.
                                            e.Cancel();
                                        }
                                    }
                                    // Handle moves and button presses that might be from wisptis or in conflict with our current state
                                    else if ((actions & (RawMouseActions.AbsoluteMove | RawMouseActions.QueryCursor |
                                                            RawMouseActions.Button1Press | RawMouseActions.Button1Release |
                                                            RawMouseActions.Button2Press | RawMouseActions.Button2Release)) != 0)
                                    {
                                        // If we see a mouse left down and stylus is inRange and we haven't sent a mouse down
                                        // then send it through.
                                        if ((actions & RawMouseActions.Button1Press) != 0 && CurrentStylusDevice != null &&
                                            !CurrentStylusDevice.InAir)
                                        {
                                            // We can only Activate the window without flashing the tray icon for it when
                                            // we are processing an Input message.  So we defer it till we see the mouse down.
                                            HwndSource hwndSource = mouseInputReport.InputSource as HwndSource;
                                            IntPtr hwnd = hwndSource != null ? hwndSource.CriticalHandle : IntPtr.Zero;

                                            // If we see a stylusdown and we are not the foreground window
                                            // and there's no capture then make sure we get activated.
                                            // We only do this for top most windows.
                                            if (hwnd != IntPtr.Zero &&
                                                 _inputManager.Value.PrimaryMouseDevice.Captured != null &&
                                                 UnsafeNativeMethods.GetParent(new HandleRef(this, hwnd)) == IntPtr.Zero &&
                                                 hwnd != UnsafeNativeMethods.GetForegroundWindow())
                                            {
                                                // Check to see if this window has the WS_EX_NOACTIVATE style set, if so don't do the activation work.
                                                int style = UnsafeNativeMethods.GetWindowLong(new HandleRef(this,hwnd), NativeMethods.GWL_EXSTYLE);

                                                if ((style & NativeMethods.WS_EX_NOACTIVATE) == 0)
                                                {
                                                    UnsafeNativeMethods.SetForegroundWindow(new HandleRef(this,hwndSource.Handle));
                                                }
                                            }

                                            // There are times we need to make sure we promote the left mouse down before we see a system gesture.
                                            // This is when the press and hold gesture is disabled and thus we can guarentee that sending the
                                            // left mouse down is the correct thing to do.  This is critical for some controls such as repeat
                                            // buttons since in order get them in the pressed state (and start them repeating) we have to send the
                                            // left mouse down.  Note if you go down with the stylus and don't move it past the drag tolerance no
                                            // system gesture will be generated and the normal code to promote the mouse down will not happen otherwise.
                                            //
                                            // This code will kick in on Vista with the new support to disable the press and hold gesture per element
                                            // (via WM_TABLE_QUERYSYSTEMGESTURESTATUS message) and also on XP and Vista if the press and hold gesture is
                                            // disabled in the tablet control panel.
                                            if (!CurrentStylusDevice.SentMouseDown && fromWisptis && ShouldPromoteToMouse(CurrentStylusDevice))
                                            {
                                                // left button down...lets replay the down at this time...
                                                // Note: We may wait till later if stylus is not down yet!
                                                // We will do it only when we are not manipulating and we will
                                                // delay it if we know that manipulations are possible.
                                                StylusTouchDevice touchDevice = CurrentStylusDevice.TouchDevice;
                                                if (touchDevice.PromotingToManipulation)
                                                {
                                                    touchDevice.StoredStagingAreaItems.AddItem(e.StagingItem);
                                                }
                                                else if (touchDevice.PromotingToOther)
                                                {
                                                    CurrentStylusDevice.PlayBackCachedDownInputReport(mouseInputReport.Timestamp);
                                                }
                                            }
                                        }

                                        // We want to eat mouse messages with the wisptis injected signature except
                                        // if the MouseDevice is getting activated or deactivated by it (filtered out
                                        // above).  We also want to eat any spurious mouse events recieved between the
                                        // stylus down and the stylus system gesture getting fired.
                                        if (fromWisptis)
                                        {
                                            // eat mouse messages generated by stylus;
                                            // these will be handled off the stylus event stream and promoted to a mouse input event
                                            bool handled = true;

                                            // If the mouse is captured we need to validate that the mouse location
                                            // is actually inside the client area (we will only see those wisptis
                                            // events and can thus eat this one).
                                            Point ptMouse = new Point(mouseInputReport.X, mouseInputReport.Y);
                                            bool stylusIsDown = (CurrentStylusDevice != null) ? !CurrentStylusDevice.InAir : false;
                                            if (!stylusIsDown && Mouse.Captured != null && !InWindowClientRect(ptMouse, mouseInputReport.InputSource))
                                            {
                                                handled = false;
                                            }

                                            // If the input has been marked as Handled, we want it to be cancelled at PreProcess stage.
                                            if (handled)
                                            {
                                                // We can't mark left and right mouse buttons as handled since it will stop the
                                                // DefWindowProc from being processed but we Cancel it which stops mouse from processing
                                                // it.  Move's though we need to eat.
                                                if ((actions & (RawMouseActions.Button1Press | RawMouseActions.Button2Press)) == 0)
                                                {
                                                    input.Handled = true;
                                                }
                                                e.Cancel();

                                                // If the stylus is in the up state when we see a mouse down then just note that we've
                                                // seen the mouse left down and wanted to send it but the stylus down
                                                // has not been seen yet so we can't.  When we see the stylus down later we'll promote
                                                // the left mouse down after processing the stylus down.
                                                if ((actions & RawMouseActions.Button1Press) != 0 && CurrentStylusDevice != null &&
                                                    CurrentStylusDevice.InAir)
                                                {
                                                    CurrentStylusDevice.SetSawMouseButton1Down(true);
                                                }

                                                // Only try to process stylus events on wisptis generated mouse events and
                                                // make sure we don't re-enter ourselves.
                                                if (!_processingQueuedEvent)
                                                {
                                                    // Make sure we process any pending Stylus Input before this mouse event.
                                                    InputManagerProcessInput(null);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            bool cancelMouseEvent = false;
                                            bool markHandled = true;

                                            // If Stylus is in range then it will be driving the mouse.  Ignore any mouse input.
                                            if (_stylusDeviceInRange)
                                            {
                                                cancelMouseEvent = true;

                                                // We can't mark left and right mouse buttons as handled since it will stop the
                                                // DefWindowProc from being processed but we Cancel it which stops mouse from processing
                                                // it.  Move's though we need to eat.
                                                if ((actions & (RawMouseActions.Button1Press | RawMouseActions.Button2Press)) == 0)
                                                {
                                                    markHandled = false;
                                                }
                                            }
                                            // If we see only a mouse move related action while the stylus is in range then
                                            // eat it or try to defer it if not currently in range to see if we come into range.
                                            else if ((actions & ~(RawMouseActions.AbsoluteMove | RawMouseActions.QueryCursor)) == 0)
                                            {
                                                if (DeferMouseMove(mouseInputReport))
                                                {
                                                    cancelMouseEvent = true;
                                                }
                                                else
                                                {
                                                    // If we now think we're going in range then eat this mouse event
                                                    if (_lastMouseMoveFromStylus && ShouldConsiderStylusInRange(mouseInputReport))
                                                    {
                                                        SendDeferredMouseEvent(false); // Make sure we clear any deferred mouse events now.
                                                        cancelMouseEvent = true;
                                                    }
                                                    // We're now allowing this mouse event (and deferred one) to be processed...
                                                    else
                                                    {
                                                        // It's a Synchronize that we are letting through so set stylus was not last move anymore.
                                                        _lastMouseMoveFromStylus = false;

                                                        // See if we are dealing with a second mouse event,
                                                        // if so force the original one it to be processed first.
                                                        if (!_triedDeferringMouseMove)
                                                            SendDeferredMouseEvent(true);

                                                        // CurrentStylusDevice is not in range and we're seeeing mouse messages
                                                        // that are not from wisptis, time to set IsStylusOver to null
                                                        if (CurrentStylusDevice != null)
                                                        {
                                                            // No current stylus device anymore either.
                                                            SelectStylusDevice(null, null, true);
                                                        }
                                                    }
                                                }
                                            }
                                            // If we see a down and have a cached move then let them both go through
                                            else
                                            {
                                                // We see a mouse button 1 or 2 down/up.  If we have a cache then dump it and mark that we've
                                                // seen mouse input.
                                                _lastMouseMoveFromStylus = false;
                                                SendDeferredMouseEvent(true);

                                                // CurrentStylusDevice is not in range and we're seeeing mouse messages
                                                // that are not from wisptis, time to set IsStylusOver to null
                                                if (CurrentStylusDevice != null)
                                                {
                                                    // No current stylus device anymore either.
                                                    SelectStylusDevice(null, null, true);
                                                }
                                            }

                                            // See if we wanted to eat this mouse event...
                                            if (cancelMouseEvent)
                                            {
                                                e.Cancel(); // abort this input
                                                if (markHandled)
                                                {
                                                    input.Handled = true; // We also don't want MouseDevice processing this.
                                                }
                                            }
                                        }
                                    }
                                    // Some other real mouse only generated event came through...
                                    else
                                    {
                                        // Make sure it's only the ones we know should come through.
                                        Debug.Assert((actions & ~(RawMouseActions.Button3Press | RawMouseActions.Button3Release |
                                                                   RawMouseActions.Button4Press | RawMouseActions.Button4Release |
                                                                   RawMouseActions.Button5Press | RawMouseActions.Button5Release |
                                                                   RawMouseActions.VerticalWheelRotate |
                                                                   RawMouseActions.HorizontalWheelRotate)) == 0);

                                        // If we are not in range then make sure we update our state.
                                        // Otherwise we just let this event go through to the MouseDevice.
                                        if (!_stylusDeviceInRange)
                                        {
                                            // We are letting this move through so set stylus was not last move anymore.
                                            _lastMouseMoveFromStylus = false;

                                            // Dump cache!
                                            SendDeferredMouseEvent(true);

                                            // CurrentStylusDevice is not in range and we're seeeing mouse messages
                                            // that are not from wisptis, time to set IsStylusOver to null
                                            if (CurrentStylusDevice != null)
                                            {
                                                // We now don't have a current stylus device.
                                                SelectStylusDevice(null, null, true);
                                            }
                                        }
                                        else
                                        {
                                            // Make sure to dump the cached mouse event if we are in
                                            // range to make sure this mouse event is at the right spot!
                                            SendDeferredMouseEvent(true);
                                        }
                                    }
                                }
                                else
                                {
                                    _lastMouseMoveFromStylus = false;
                                }
                            }
                            else
                            {
                                // This event is marked as coming from a StylusDevice so make sure we update flag that we saw mouse event from stylus.
                                _lastMouseMoveFromStylus = true;

                                RawMouseInputReport rawMouseInputReport = (RawMouseInputReport) input.Report;
                                StylusDevice stylusDevice = (StylusDevice)input.Device;
                                if (!stylusDevice.InRange && rawMouseInputReport._isSynchronize)
                                {
                                    // eat this one because it is from an activate.
                                    e.Cancel();
                                    input.Handled = true;
                                }
                            }
                        }
                        else if (input.Report.Type == InputType.Stylus)
                        {
                            RawStylusInputReport stylusInputReport = (RawStylusInputReport) input.Report;
                            StylusDevice stylusDevice = stylusInputReport.StylusDevice; // RTI sets this if it finds StylusDevice based on Id.
                            bool cancelInput = true; // Only process if we see we have valid input data.

                            if (stylusInputReport.InputSource != null && stylusInputReport.PenContext != null)
                            {
                                if (stylusDevice == null)
                                {
                                    // look up stylus device, select it in the Stylus, and claim input for it
                                    stylusDevice = FindStylusDevice(stylusInputReport.StylusDeviceId);

                                    // Try refreshing tablets if we failed to find this stylus device.
                                    if (stylusDevice == null)
                                    {
                                        stylusDevice = TabletDevices.UpdateStylusDevices(
                                                                    stylusInputReport.TabletDeviceId,
                                                                    stylusInputReport.StylusDeviceId);
                                    }

                                    stylusInputReport.StylusDevice = stylusDevice; // update stylusdevice.
                                }

                                _triedDeferringMouseMove = false; // reset anytime we see stylus input.

                                // See if this is the special InRange input report that we use to track queued inrange
                                // events so that we can better filter out bogus mouse input.
                                if (stylusInputReport.Actions == RawStylusActions.InRange && stylusInputReport.Data == null)
                                {
                                    stylusInputReport.PenContext.DecrementQueuedInRangeCount();
                                    e.Cancel();
                                    input.Handled = true;
                                    _lastInRangeTime = Environment.TickCount;
                                    return;
                                }

                                // See if this is the special DoubleTap Gesture input report.  We use this
                                // event to know when we won't get the tap or drag gesture while the stylus
                                // is down.  This allows us to detect and generate the Drag gesture on our own.
                                if (stylusInputReport.Actions == RawStylusActions.SystemGesture && stylusDevice != null)
                                {
                                    RawStylusSystemGestureInputReport systemGestureReport = (RawStylusSystemGestureInputReport)stylusInputReport;
                                    if (systemGestureReport.SystemGesture == RawStylusSystemGestureInputReport.InternalSystemGestureDoubleTap)
                                    {
                                        stylusDevice.SeenDoubleTapGesture = true;
                                        e.Cancel();
                                        input.Handled = true;
                                        return;
                                    }
                                }

                                if (stylusDevice != null && IsValidStylusAction(stylusInputReport))
                                {
                                    cancelInput = false; // We can process this event - don't cancel!

                                    // See if a static gesture can be generated
                                    TabletDevice tabletDevice = stylusDevice.TabletDevice;
                                    if (tabletDevice != null)
                                    {
                                        SystemGesture? systemGesture = tabletDevice.GenerateStaticGesture(stylusInputReport);
                                        if (systemGesture != null)
                                        {
                                            GenerateGesture(stylusInputReport, systemGesture.Value);
                                        }
                                    }

                                    // See if we need to generate a tap gesture.
                                    if (stylusInputReport.Actions == RawStylusActions.Up)
                                    {
                                        if (!stylusDevice.GestureWasFired)
                                        {
                                            GenerateGesture(stylusInputReport, stylusDevice.LastTapBarrelDown ? SystemGesture.RightTap : SystemGesture.Tap);
                                        }

                                        if (!_inDragDrop && !stylusInputReport.PenContext.Contexts.IsWindowDisabled)
                                        {
                                            // We need to process a MouseMove before promoting a MouseUp (in PromoteMainToMouse)
                                            // since the stylus updates the button states for mouse to up then.
                                            // Note: The Stylus Up is at the same location as the last stylus move so this is OK to do here.
                                            ProcessMouseMove(stylusDevice, stylusInputReport.Timestamp, false);
                                        }
                                    }

                                    input.Device = stylusDevice;
                                }
                            }

                            if (cancelInput)
                            {
                                e.Cancel();  // Don't process this bogus event any further.
                            }
                        }
                    }
                }
            }
        }

        /////////////////////////////////////////////////////////////////////
        ///<SecurityNote>
        /// Critical: Accesses SecurityCriticalData e.StagingItem.Input and _inputManager.Value.
        ///           Calls SecurityCritical methods: StylusDevice.UpdateStateForSystemGesture,
        ///              InputEventArgs.Handled, StylusDevice.UpdateInRange, StylusDevice.UpdateState,
        ///              RawStylusInputReport.PenContext, SelectStylusDevice, VerifyStylusPlugInCollectionTarget,
        ///              ProcessMouseMove, and CallPlugInsForMouse.
        ///            It can also be used for Input spoofing.
        ///</SecurityNote>
        [SecurityCritical]
        private void PreNotifyInput(object sender, NotifyInputEventArgs e)
        {
            if(e.StagingItem.Input.RoutedEvent == InputManager.PreviewInputReportEvent)
            {
                InputReportEventArgs inputReportEventArgs = e.StagingItem.Input as InputReportEventArgs;

                if(!inputReportEventArgs.Handled && inputReportEventArgs.Report.Type == InputType.Stylus)
                {
                    RawStylusInputReport rawStylusInputReport = (RawStylusInputReport) inputReportEventArgs.Report;
                    StylusDevice stylusDevice = rawStylusInputReport.StylusDevice;

                    if (stylusDevice != null)
                    {
                        // update stylus device state (unless this is exclusively system gesture or
                        // in-range/out-of-range event - which don't carry much info)
                        switch (rawStylusInputReport.Actions)
                        {
                            case RawStylusActions.SystemGesture:
                                stylusDevice.UpdateStateForSystemGesture(
                                    (RawStylusSystemGestureInputReport)rawStylusInputReport);
                                break;
                            case RawStylusActions.OutOfRange:
                                _lastInRangeTime = Environment.TickCount;
                                stylusDevice.UpdateInRange(false, rawStylusInputReport.PenContext);
                                UpdateIsStylusInRange(false);
                                break;
                            case RawStylusActions.InRange:
                                _lastInRangeTime = Environment.TickCount;
                                stylusDevice.UpdateInRange(true, rawStylusInputReport.PenContext);
                                stylusDevice.UpdateState(rawStylusInputReport);
                                UpdateIsStylusInRange(true);
                                _lastRawMouseAction = RawMouseActions.None; // make sure we promote a mouse move on next event.
                                break;
                            default: // InAirMove, Down, Move, Up go through here.
                                stylusDevice.UpdateState(rawStylusInputReport);
                                break;
                        }

                        // Can only update Over state if not in a DragDrop operation!!
                        if (!_inDragDrop && !rawStylusInputReport.PenContext.Contexts.IsWindowDisabled && !stylusDevice.IgnoreStroke)
                        {
                            Point position = stylusDevice.GetRawPosition(null);
                            position = DeviceUnitsFromMeasureUnits(position); // change back to device coords.
                            IInputElement target = stylusDevice.FindTarget(stylusDevice.CriticalActiveSource, position);
                            SelectStylusDevice(stylusDevice, target, true);
                        }
                        else
                        {
                            SelectStylusDevice(stylusDevice, null, false); // don't update over.
                        }

                        // If this is a stylus down and we don't have a valid target then the stylus went down
                        // on the wrong window (a transparent window handling bug in wisptis).  In this case
                        // we want to ignore all stylus input until after the next stylus up.
                        if (rawStylusInputReport.Actions == RawStylusActions.Down && stylusDevice.Target == null)
                        {
                            stylusDevice.IgnoreStroke = true;
                        }

                        // Tell the InputManager that the MostRecentDevice is us.
                        _inputManager.Value.MostRecentInputDevice = stylusDevice;

                        // Verify that we sent the real time stylus events to the proper plugincollection.
                        VerifyStylusPlugInCollectionTarget(rawStylusInputReport);
                    }
                }
            }

            // During the PreviewStylusDown event, we update the tap count, if there are
            // multiple "quick" taps in approximately the "same" location (as defined
            // by the hosting environment, aka the registry).
            if(e.StagingItem.Input.RoutedEvent == Stylus.PreviewStylusDownEvent)
            {
                StylusEventArgs stylusDownEventArgs = e.StagingItem.Input as StylusDownEventArgs;

                StylusDevice stylusDevice = stylusDownEventArgs.StylusDevice;

                if (stylusDevice != null)
                {
                    Point ptClient = stylusDevice.GetRawPosition(null);

                    // determine barrel state...
                    bool bBarrelPressed = false;
                    int barrelPos =
                        stylusDevice.TabletDevice.StylusPointDescription.GetButtonBitPosition(StylusPointProperties.BarrelButton);
                    if (barrelPos != -1
                        && stylusDevice.StylusButtons[barrelPos].StylusButtonState == StylusButtonState.Down)
                    {
                        bBarrelPressed = true;
                    }

                    Point pPixelPoint = DeviceUnitsFromMeasureUnits(ptClient);
                    Point pLastPixelPoint = DeviceUnitsFromMeasureUnits(stylusDevice.LastTapPoint);

                    // How long since the last click? (deals with tickcount wrapping too)
                    //  Here's some info on how this works...
                    //      int.MaxValue - int.MinValue = -1 (subtracting any negative # from MaxValue keeps this negative)
                    //      int.MinValue - int.MaxValue = 1 (subtracting any positive # from MinValue keeps this positive)
                    //  So as the values get farther apart from MaxInt and MinInt the difference grows which is what we want.
                    //  We use Abs to ensure if we get older time coming through here (not expected) we'll do better
                    //  at filtering it out if delta is greater than the double tap time.  We should always see
                    //  MinInt - MaxInt which will produce a positive number when wrapping happens.
                    int  timeSpan = Math.Abs(unchecked(stylusDownEventArgs.Timestamp - stylusDevice.LastTapTime));
                    // Is the delta coordinates of this tap close enough to the last tap?

                    Size doubleTapSize = stylusDevice.TabletDevice.DoubleTapSize;
                    bool isSameSpot = (Math.Abs(pPixelPoint.X - pLastPixelPoint.X) < doubleTapSize.Width) &&
                                        (Math.Abs(pPixelPoint.Y - pLastPixelPoint.Y) < doubleTapSize.Height);

                    // Now check everything to see if this is a multi-click.
                    if (timeSpan < _doubleTapDeltaTime
                        && isSameSpot
                        && (bBarrelPressed == stylusDevice.LastTapBarrelDown) )
                    {
                        // Yes, increment the count
                        stylusDevice.TapCount++;
                    }
                    else
                    {
                        // No, not a multi-click, reset everything.
                        stylusDevice.TapCount = 1;
                        stylusDevice.LastTapPoint  = new Point(ptClient.X, ptClient.Y);
                        stylusDevice.LastTapTime = stylusDownEventArgs.Timestamp;
                        stylusDevice.LastTapBarrelDown = bBarrelPressed;
                    }

                    // Make sure to update the mouse location on stylus down.
                    ProcessMouseMove(stylusDevice, stylusDownEventArgs.Timestamp, false);
                }
            }
        }

        /////////////////////////////////////////////////////////////////////

        ///<SecurityNote>
        ///     Critical - calls a critical method (PromoteRawToPreview, MouseDevice.CriticalActiveSource,
        ///                 InputReport.InputSource, PromotePreviewToMain, UpdateButtonStates,
        ///                 PromoteMainToMouse and GenerateGesture)
        ///              - accesses e.StagingItem.Input, _inputManager.Value and TabletDevices.
        ///              It can also be used for Input spoofing.
        ///</SecurityNote>
        [SecurityCritical ]
        private void PostProcessInput(object sender, ProcessInputEventArgs e)
        {
            //only [....] with mouse capture if we're enabled, or else there are no tablet devices
            //hence no input.  We have to work around this because getting the
            //Tablet.TabletDevices will load Penimc.dll.
            if (_inputEnabled)
            {
                // Watch the LostMouseCapture and GotMouseCapture events to keep stylus capture in [....].
                if(e.StagingItem.Input.RoutedEvent == Mouse.LostMouseCaptureEvent ||
                    e.StagingItem.Input.RoutedEvent == Mouse.GotMouseCaptureEvent)
                {
#if MULTICAPTURE
                    var mouseStylusDevice = Mouse.PrimaryDevice.StylusDevice;
#endif

                    // Make sure mouse and stylus capture is the same.
                    foreach (TabletDevice tabletDevice in TabletDevices)
                    {
                        foreach (StylusDevice stylusDevice in tabletDevice.StylusDevices)
                        {
#if MULTICAPTURE
                            if (stylusDevice == mouseStylusDevice)
                            {
#endif
                                // We use the Mouse device state for each call just in case we
                                // get reentered in the middle of changing so when we continue
                                // we'll use the current mouse capture state (which should NOP).
                                stylusDevice.Capture(Mouse.Captured, Mouse.CapturedMode);
#if MULTICAPTURE
                            }
#endif
                        }
                    }
                }
            }

            if(e.StagingItem.Input.RoutedEvent == InputManager.InputReportEvent && !_inDragDrop)
            {
                InputReportEventArgs input = e.StagingItem.Input as InputReportEventArgs;
                if(!input.Handled && input.Report.Type == InputType.Stylus)
                {
                    RawStylusInputReport report = (RawStylusInputReport) input.Report;
                    // Only promote if the window is enabled!
                    if (!report.PenContext.Contexts.IsWindowDisabled)
                    {
                        PromoteRawToPreview(report, e);

                        // Need to reset this flag at the end of StylusUp processing.
                        if (report.Actions == RawStylusActions.Up)
                        {
                            report.StylusDevice.IgnoreStroke = false;
                        }
                    }
                    else
                    {
                        // We don't want to send input messages to a disabled window, but if this
                        // is a StylusUp action then we need to make sure that the device knows it
                        // is no longer active.  If we don't do this, we will incorrectly think this
                        // device is still active, and so therefore no other touch input will be
                        // considered "primary" input, causing it to be ignored for most actions
                        // (like button clicks).  (DevDiv2 520639)
                        if ((report.Actions & RawStylusActions.Up) != 0 && report.StylusDevice != null)
                        {
                            StylusTouchDevice touchDevice = report.StylusDevice.TouchDevice;
                            // Don't try to deactivate if the device isn't active.  This can happen if
                            // the window was disabled for the touch-down as well, in which case we
                            // never activated the device and therefore don't need to deactivate it.
                            if (touchDevice.IsActive)
                            {
                                touchDevice.OnDeactivate();
                            }
                        }
                    }
                }
            }

            // If we are processing an OutOfRange event then see if we need to update the over state.
            // We need to update it if mouse is already outside the window (MouseDevice.DirectlyOver
            // is null) since if it has already seen the WM_MOUSELEAVE we'll never update out over
            // state properly.  If the WM_MOUSELEAVE comes in after we see the OutOfRange then the
            // code at the end of PreProcessInput will deal that case properly.
            if(e.StagingItem.Input.RoutedEvent == Stylus.StylusOutOfRangeEvent)
            {
                RawMouseInputReport mouseDeactivateInputReport = _mouseDeactivateInputReport;
                _mouseDeactivateInputReport = null;
                StylusEventArgs eventArgsOutOfRange = (StylusEventArgs)e.StagingItem.Input;

                // If we have deferred mouse moves then make sure we process last one now.
                if (_lastRawMouseAction == RawMouseActions.AbsoluteMove && _waitingForDelegate)
                {
                    ProcessMouseMove(eventArgsOutOfRange.StylusDevice, eventArgsOutOfRange.Timestamp, false);
                }

                // See if we need to set the Mouse Activate flag.
                PresentationSource mouseSource = _inputManager.Value.PrimaryMouseDevice.CriticalActiveSource;

                // See if we need to change the stylus over state state and send a mouse deactivate.
                // We send the cached Deactivate through if we saw mouse deactivate before out of range event
                // *or* for a quick move with the stylus over a window we may not even see any win32 mouse events
                // so in that case we also need to deactivate the mouse since we were the ones that activated it.
                if (mouseDeactivateInputReport != null || (!_seenRealMouseActivate && mouseSource != null))
                {
                    // First update the StylusDevice DirectlyOver to null if the mouse device saw a Deactivate (means
                    // the mouse left the window) or if it never saw a real activate (stylus mouse promotion
                    // caused it to be active).
                    eventArgsOutOfRange.StylusDevice.ChangeStylusOver(null);

                    // Now send the mouse deactivate
                    RawMouseInputReport newMouseInputReport = mouseDeactivateInputReport != null ?
                                                    new RawMouseInputReport(
                                                        mouseDeactivateInputReport.Mode,
                                                        eventArgsOutOfRange.Timestamp, // updated time
                                                        mouseDeactivateInputReport.InputSource,
                                                        mouseDeactivateInputReport.Actions,
                                                        mouseDeactivateInputReport.X,
                                                        mouseDeactivateInputReport.Y,
                                                        mouseDeactivateInputReport.Wheel,
                                                        mouseDeactivateInputReport.ExtraInformation) :
                                                    new RawMouseInputReport(
                                                        InputMode.Foreground,
                                                        eventArgsOutOfRange.Timestamp, // updated time
                                                        mouseSource,
                                                        RawMouseActions.Deactivate,
                                                        0,
                                                        0,
                                                        0,
                                                        IntPtr.Zero);

                    InputReportEventArgs actionsArgs = new InputReportEventArgs(eventArgsOutOfRange.StylusDevice, newMouseInputReport);
                    actionsArgs.RoutedEvent=InputManager.PreviewInputReportEvent;
                    _inputManager.Value.ProcessInput(actionsArgs);
                }
            }

            // Deal with sending mouse events to the plugins.
            // NOTE: We want to do this after the mousedevice has sent it's click through
            // events (PreviewMouseDownOutsideCapturedElementEvent/PreviewMouseUpOutsideCapturedElementEvent)
            // and PreviewMouse events so that we can route more accurately to where the Mouse events will
            // actually get routed.
            CallPlugInsForMouse(e);

            PromotePreviewToMain(e);

            UpdateButtonStates(e);

            PromoteMainToOther(e);

            // See if we need to generate a drag gesture.
            if(e.StagingItem.Input.RoutedEvent == Stylus.StylusMoveEvent)
            {
                StylusEventArgs stylusMove = (StylusEventArgs)e.StagingItem.Input;
                if (stylusMove.StylusDevice.SeenDoubleTapGesture && !stylusMove.StylusDevice.GestureWasFired &&
                    stylusMove.StylusDevice.DetectedDrag)
                {
                    GenerateGesture(stylusMove.InputReport, SystemGesture.Drag);
                }
            }

            // Process the flick scroll up/down system gesture now.
            if (e.StagingItem.Input.RoutedEvent == Stylus.StylusSystemGestureEvent)
            {
                StylusSystemGestureEventArgs stylusSystemGesture = (StylusSystemGestureEventArgs)e.StagingItem.Input;
                if (stylusSystemGesture.SystemGesture == SystemGesture.Flick)
                {
                    HandleFlick(stylusSystemGesture.ButtonState, stylusSystemGesture.StylusDevice.DirectlyOver);
                }
            }
        }

        /////////////////////////////////////////////////////////////////////

        ///<SecurityNote>
        /// Critical - calls a critical function ( PushInput)
        ///          - called by PostProcessInput
        ///</SecurityNote>
        [SecurityCritical ]
        void PromoteRawToPreview(RawStylusInputReport report, ProcessInputEventArgs e)
        {
            RoutedEvent routedEvent = GetPreviewEventFromRawStylusActions(report.Actions);
            if (routedEvent != null && report.StylusDevice != null && !report.StylusDevice.IgnoreStroke)
            {
                StylusEventArgs args;
                if (routedEvent != Stylus.PreviewStylusSystemGestureEvent)
                {
                    if (routedEvent == Stylus.PreviewStylusDownEvent)
                    {
                        args = new StylusDownEventArgs(report.StylusDevice, report.Timestamp);
                    }
                    else
                    {
                        args = new StylusEventArgs(report.StylusDevice, report.Timestamp);
                     }
                }
                else
                {
                    RawStylusSystemGestureInputReport reportSg = (RawStylusSystemGestureInputReport)report;
                    args = new StylusSystemGestureEventArgs(report.StylusDevice,
                                                            report.Timestamp,
                                                            reportSg.SystemGesture,
                                                            reportSg.GestureX,
                                                            reportSg.GestureY,
                                                            reportSg.ButtonState);
                }
                args.InputReport = report;
                args.RoutedEvent= routedEvent;
                e.PushInput(args, e.StagingItem);
            }
        }

        /////////////////////////////////////////////////////////////////////

        ///<SecurityNote>
        /// Critical - calls a critical method ( pushInput).
        ///           - called by PostProcessInput
        ///</SecurityNote>
        [SecurityCritical]
        void PromotePreviewToMain(ProcessInputEventArgs e)
        {
            if (!e.StagingItem.Input.Handled)
            {
                RoutedEvent eventMain = GetMainEventFromPreviewEvent(e.StagingItem.Input.RoutedEvent);
                if (eventMain != null)
                {
                    StylusEventArgs eventArgsPreview = (StylusEventArgs)e.StagingItem.Input;
                    StylusDevice stylusDevice = eventArgsPreview.InputReport.StylusDevice;

                    StylusEventArgs eventArgsMain;
                    if (eventMain == Stylus.StylusDownEvent ||
                        eventMain == Stylus.PreviewStylusDownEvent)
                    {
                        StylusDownEventArgs downEventArgsPreview = (StylusDownEventArgs)eventArgsPreview;
                        eventArgsMain = new StylusDownEventArgs(stylusDevice, eventArgsPreview.Timestamp);
                    }
                    else if (eventMain == Stylus.StylusButtonDownEvent ||
                        eventMain == Stylus.StylusButtonUpEvent)
                    {
                        StylusButtonEventArgs buttonEventArgsPreview = (StylusButtonEventArgs)eventArgsPreview;
                        eventArgsMain = new StylusButtonEventArgs(stylusDevice, eventArgsPreview.Timestamp, buttonEventArgsPreview.StylusButton);
                    }
                    else if (eventMain != Stylus.StylusSystemGestureEvent)
                    {
                        eventArgsMain = new StylusEventArgs(stylusDevice, eventArgsPreview.Timestamp);
                    }
                    else
                    {
                        StylusSystemGestureEventArgs previewSystemGesture = (StylusSystemGestureEventArgs)eventArgsPreview;
                        eventArgsMain = new StylusSystemGestureEventArgs(stylusDevice,
                                                                         previewSystemGesture.Timestamp,
                                                                         previewSystemGesture.SystemGesture,
                                                                         previewSystemGesture.GestureX,
                                                                         previewSystemGesture.GestureY,
                                                                         previewSystemGesture.ButtonState);
                    }
                    eventArgsMain.InputReport = eventArgsPreview.InputReport;
                    eventArgsMain.RoutedEvent = eventMain;
                    e.PushInput(eventArgsMain, e.StagingItem);
                }
            }
            else
            {
                // A TouchDevice is activated before TouchDown and deactivated after TouchUp
                // But if PreviewStylusUp event is handled by the user, it will
                // never be promoted to TouchUp event leaving the TouchDevice in inconsistent
                // active state. Hence deactivating touch device if it is active.
                StylusEventArgs stylusEventArgs = e.StagingItem.Input as StylusEventArgs;
                if (stylusEventArgs != null &&
                    stylusEventArgs.RoutedEvent == Stylus.PreviewStylusUpEvent &&
                    stylusEventArgs.StylusDevice.TouchDevice.IsActive)
                {
                    stylusEventArgs.StylusDevice.TouchDevice.OnDeactivate();
                }
            }
        }

        /// <SecurityNote>
        ///     Critical: Calls SecurityCritical methods.
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainToOther(ProcessInputEventArgs e)
        {
            StagingAreaInputItem stagingItem = e.StagingItem;
            StylusEventArgs stylusEventArgs = stagingItem.Input as StylusEventArgs;
            if (stylusEventArgs == null)
            {
                return;
            }
            StylusDevice stylusDevice = stylusEventArgs.StylusDevice;
            StylusTouchDevice touchDevice = stylusDevice.TouchDevice;
            bool shouldPromoteToMouse = ShouldPromoteToMouse(stylusDevice);

            if (IsTouchPromotionEvent(stylusEventArgs))
            {
                if (e.StagingItem.Input.Handled)
                {
                    // A TouchDevice is activated before TouchDown and deactivated after TouchUp
                    // But if StylusUp event is handled by the user, it will
                    // never be promoted to TouchUp event leaving the TouchDevice in inconsistent
                    // active state. Hence deactivating touch device if it is active.
                    if (stylusEventArgs.RoutedEvent == Stylus.StylusUpEvent &&
                        touchDevice.IsActive)
                    {
                        touchDevice.OnDeactivate();
                    }
                }
                else
                {
                    // This event is to also route as a Touch event.
                    // PromoteMainToMouse will eventually see the resulting
                    // touch event when it finishes and promote to mouse.
                    PromoteMainToTouch(e, stylusEventArgs);
                }
            }
            else if (e.StagingItem.Input.RoutedEvent == Stylus.StylusSystemGestureEvent)
            {
                // Promote stylus system gesture to mouse if needed or
                // store them if it cannot be determined that we are manipulating
                // at this stage.
                if (shouldPromoteToMouse)
                {
                    if (touchDevice.PromotingToManipulation)
                    {
                        touchDevice.StoredStagingAreaItems.AddItem(stagingItem);
                    }
                    else if (touchDevice.PromotingToOther)
                    {
                        PromoteMainToMouse(stagingItem);
                    }
                }
            }
            else if(shouldPromoteToMouse && touchDevice.PromotingToOther)
            {
                // This is not a touch event, go to mouse
                PromoteMainToMouse(stagingItem);
            }
        }

        /// <SecurityNote>
        ///     Critical: Accesses e.StagingItem.Input.
        /// </SecurityNote>
        [SecurityCritical]
        private static bool IsTouchPromotionEvent(StylusEventArgs stylusEventArgs)
        {
            if (stylusEventArgs != null)
            {
                RoutedEvent routedEvent = stylusEventArgs.RoutedEvent;
                return (IsTouchStylusDevice(stylusEventArgs.StylusDevice) &&
                        (routedEvent == Stylus.StylusMoveEvent ||
                         routedEvent == Stylus.StylusDownEvent ||
                         routedEvent == Stylus.StylusUpEvent));

            }
            return false;
        }

        private static bool IsTouchStylusDevice(StylusDevice stylusDevice)
        {
            return (stylusDevice.TabletDevice != null &&
                stylusDevice.TabletDevice.Type == TabletDeviceType.Touch);
        }

        /// <SecurityNote>
        ///     Critical: Calls PromoteMainMoveToTouch, PromoteMainDownToTouch or PromoteMainUpToTouch
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainToTouch(ProcessInputEventArgs e, StylusEventArgs stylusEventArgs)
        {
            StylusDevice stylusDevice = stylusEventArgs.StylusDevice;
            stylusDevice.UpdateTouchActiveSource();

            if (stylusEventArgs.RoutedEvent == Stylus.StylusMoveEvent)
            {
                PromoteMainMoveToTouch(stylusDevice, e.StagingItem);
            }
            else if (stylusEventArgs.RoutedEvent == Stylus.StylusDownEvent)
            {
                PromoteMainDownToTouch(stylusDevice, e.StagingItem);
            }
            else if (stylusEventArgs.RoutedEvent == Stylus.StylusUpEvent)
            {
                PromoteMainUpToTouch(stylusDevice, e.StagingItem);
            }
        }

        /// <SecurityNote>
        ///     Critical: Calls PromoteMainToMouse
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainDownToTouch(StylusDevice stylusDevice, StagingAreaInputItem stagingItem)
        {
            StylusTouchDevice touchDevice = stylusDevice.TouchDevice;
            if (touchDevice.IsActive)
            {
                // Deactivate and end the previous cycle if already active
                touchDevice.OnDeactivate();
            }
            touchDevice.OnActivate();
            bool shouldPromoteToMouse = ShouldPromoteToMouse(stylusDevice);
            if (!touchDevice.OnDown() && shouldPromoteToMouse)
            {
                if (touchDevice.PromotingToManipulation)
                {
                    touchDevice.StoredStagingAreaItems.AddItem(stagingItem);
                }
                else if (touchDevice.PromotingToOther)
                {
                    PromoteMainToMouse(stagingItem);
                }
            }
        }

        /// <SecurityNote>
        ///     Critical: Calls PromoteMainToMouse
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainMoveToTouch(StylusDevice stylusDevice, StagingAreaInputItem stagingItem)
        {
            StylusTouchDevice touchDevice = stylusDevice.TouchDevice;
            bool shouldPromoteToMouse = ShouldPromoteToMouse(stylusDevice);
            if (touchDevice.IsActive)
            {
                if (!touchDevice.OnMove() && shouldPromoteToMouse)
                {
                    if (touchDevice.PromotingToManipulation)
                    {
                        StagingAreaInputItemList storedStagingItems = touchDevice.StoredStagingAreaItems;
                        int stagingItemCount = storedStagingItems.Count;
                        if (stagingItemCount > 0 &&
                            storedStagingItems[stagingItemCount - 1].Input.RoutedEvent == Stylus.StylusMoveEvent)
                        {
                            storedStagingItems[stagingItemCount - 1] = stagingItem;
                            storedStagingItems.IncrementVersion();
                        }
                        else
                        {
                            touchDevice.StoredStagingAreaItems.AddItem(stagingItem);
                        }
                    }
                    else if (touchDevice.PromotingToOther)
                    {
                        PromoteMainToMouse(stagingItem);
                    }
                }
            }
            else if (shouldPromoteToMouse)
            {
                PromoteMainToMouse(stagingItem);
            }
        }

        /// <SecurityNote>
        ///     Critical: Calls PromoteMainToMouse
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainUpToTouch(StylusDevice stylusDevice, StagingAreaInputItem stagingItem)
        {
            StylusTouchDevice touchDevice = stylusDevice.TouchDevice;
            bool shouldPromoteToMouse = ShouldPromoteToMouse(stylusDevice);
            if (touchDevice.IsActive)
            {
                touchDevice.OnUp();

                bool promotingToOther = touchDevice.PromotingToOther;

                // PromoteMainToMouse is an outbound call that may have a nested message pump.
                // Hence deactivate the touch device before calling it incase it
                // turns out to be a blocking call.
                if (touchDevice.IsActive)   // OnUp may also pump messages and deactivate the touch device
                {
                    touchDevice.OnDeactivate();
                }

                // Promote Up to Mouse if mouse left/right button is
                // pressed, even if TouchUp event is handled. This is such
                // that we dont leave mouse in an inconsistent pressed state.
                if (shouldPromoteToMouse && promotingToOther &&
                    (_mouseLeftButtonState == MouseButtonState.Pressed ||
                    _mouseRightButtonState == MouseButtonState.Pressed))
                {
                    PromoteMainToMouse(stagingItem);
                }
            }
            else if (shouldPromoteToMouse)
            {
                PromoteMainToMouse(stagingItem);
            }
        }

        /// <SecurityNote>
        ///     Critical: Calls PromoteMainToMouse
        /// </SecurityNote>
        [SecurityCritical]
        internal void PromoteStoredItemsToMouse(StylusTouchDevice touchDevice)
        {
            if (!ShouldPromoteToMouse(touchDevice.StylusDevice))
            {
                return;
            }

            int count = touchDevice.StoredStagingAreaItems.Count;
            if (count > 0)
            {
                // copy the staging items, to avoid re-entrancy problems (Dev11 390405)
                StagingAreaInputItemList list = touchDevice.StoredStagingAreaItems;
                StagingAreaInputItem[] storedItems = new StagingAreaInputItem[count];
                list.CopyTo(storedItems, 0);
                list.Clear();

                // if the list's version changes during the loop, abandon the remaining
                // items.  This means input arrived re-entrantly.
                long version = list.IncrementVersion();

                for (int i = 0; i < count && version == list.Version; i++)
                {
                    // A stored item could be a Stylus input staging item queued to be promoted to mouse
                    // OR a raw mouse input report staging item for Button1Press delayed from
                    // StylusLogic.PreProcessInput. If this staging item is of such a raw mouse
                    // input report call StylusDevice's PlaybackCachedDownInputReport OR else
                    // call PromoteMainToMouse method.
                    StagingAreaInputItem stagingItem = storedItems[i];
                    InputReportEventArgs inputReportArgs = stagingItem.Input as InputReportEventArgs;
                    if (inputReportArgs != null &&
                        inputReportArgs.Report.Type == InputType.Mouse &&
                        !(inputReportArgs.Device is StylusDevice))
                    {
                        touchDevice.StylusDevice.PlayBackCachedDownInputReport(inputReportArgs.Report.Timestamp);
                    }
                    else
                    {
                        PromoteMainToMouse(stagingItem);
                    }
                }
            }
        }

        private bool ShouldPromoteToMouse(StylusDevice stylusDevice)
        {
            if (CurrentMousePromotionStylusDevice == null ||
                CurrentMousePromotionStylusDevice == stylusDevice)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        ///     The stylusdevice object which should promote to mouse.
        ///     Should promote to mouse if this is null.
        ///     This could also a dummy object to avoid promotion at all.
        /// </summary>
        internal object CurrentMousePromotionStylusDevice
        {
            get;
            set;
        }


        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: accesses stagingItem.Input
        ///           - called by PostProcessInput
        /// </SecurityNote>
        [SecurityCritical]
        private void PromoteMainToMouse(StagingAreaInputItem stagingItem)
        {
            if(!stagingItem.Input.Handled)
            {
                StylusEventArgs stylusArgs = stagingItem.Input as StylusEventArgs;
                if (stylusArgs != null)
                {
                    StylusDevice stylusDevice = stylusArgs.StylusDevice;

                    // We only want to promote to mouse when we actually have real stylus input.
                    if (stylusDevice != null)
                    {
                        Debug.Assert(ShouldPromoteToMouse(stylusDevice) && stylusDevice.TouchDevice.PromotingToOther);

                        if (IgnoreGestureToMousePromotion(stylusArgs as StylusSystemGestureEventArgs, stylusDevice.TouchDevice))
                        {
                            return;
                        }

                        RawMouseActions actions = stylusDevice.GetMouseActionsFromStylusEventAndPlaybackCachedDown(stagingItem.Input.RoutedEvent, stylusArgs);

                        if (actions != RawMouseActions.None)
                        {
                            PresentationSource mouseInputSource = stylusDevice.GetMousePresentationSource();

                            if (mouseInputSource != null)
                            {
                                Point pt = PointUtil.ScreenToClient(stylusDevice.LastMouseScreenPoint, mouseInputSource);

                                //
                                // use the dispatcher as a way of coalescing mouse *move* messages
                                // BUT don't flood the dispatcher with delegates if we're already
                                // waiting for a callback
                                //
                                if ((actions & RawMouseActions.AbsoluteMove) != 0)
                                {
                                    if (actions == _lastRawMouseAction && _waitingForDelegate)
                                    {
                                        return; // We don't need to process this one.
                                    }
                                    else
                                    {
                                        //set the waiting bit so we won't enter here again
                                        //until we get the callback
                                        _waitingForDelegate = true;

                                        Dispatcher.BeginInvoke(DispatcherPriority.Input,
                                        (DispatcherOperationCallback)delegate(object unused)
                                        {
                                            //reset our flags here in the callback.
                                            _waitingForDelegate = false;
                                            return null;
                                        },
                                        null);
                                    }
                                }

                                // See if we need to set the Mouse Activate flag.
                                if (_inputManager.Value.PrimaryMouseDevice.CriticalActiveSource != mouseInputSource)
                                {
                                    actions |= RawMouseActions.Activate;
                                }

                                _lastRawMouseAction = actions;

                                RawMouseInputReport mouseInputReport = new RawMouseInputReport(
                                                                            InputMode.Foreground, stylusArgs.Timestamp, mouseInputSource,
                                                                            actions, (int)pt.X, (int)pt.Y, 0, IntPtr.Zero);

                                InputReportEventArgs inputReportArgs = new InputReportEventArgs(stylusDevice, mouseInputReport);
                                inputReportArgs.RoutedEvent=InputManager.PreviewInputReportEvent;
                                _inputManager.Value.ProcessInput(inputReportArgs);
                            }
                        }
                    }
                }
            }
        }

        private bool IgnoreGestureToMousePromotion(StylusSystemGestureEventArgs gestureArgs, StylusTouchDevice touchDevice)
        {
            if (gestureArgs != null && touchDevice.DownHandled)
            {
                // If touchDevice's down event is already handled
                // and if this gesture is a candidate for mouse
                // left button down promotion, then such a
                // promotion should be ignored.
                SystemGesture gesture = gestureArgs.SystemGesture;
                if (gesture == SystemGesture.Tap ||
                    gesture == SystemGesture.Drag)
                {
                    return true;
                }
            }
            return false;
        }


        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: - accesses e.StagingItem.Input and PresentationSource.CriticalFromVisual
        ///           - called by PreProcessInput
        /// </SecurityNote>
        [SecurityCritical]
        void CallPlugInsForMouse(ProcessInputEventArgs e)
        {
            if (!e.StagingItem.Input.Handled)
            {
                // if we see a preview mouse event that is not generated by a stylus
                // then send on to plugin
                if ((e.StagingItem.Input.RoutedEvent != Mouse.PreviewMouseDownEvent) &&
                    (e.StagingItem.Input.RoutedEvent != Mouse.PreviewMouseUpEvent) &&
                    (e.StagingItem.Input.RoutedEvent != Mouse.PreviewMouseMoveEvent) &&
                    (e.StagingItem.Input.RoutedEvent != InputManager.InputReportEvent))
                    return;

                // record the mouse capture for later reference..
                MouseDevice mouseDevice;
                PresentationSource source;
                bool leftButtonDown;
                bool rightButtonDown;
                RawStylusActions stylusActions = RawStylusActions.None;
                int timestamp;

                // See if we need to deal sending a leave due to this PresentationSource being Deactivated
                // If not then we just return and do nothing.
                if (e.StagingItem.Input.RoutedEvent == InputManager.InputReportEvent)
                {
                    if (_activeMousePlugInCollection == null || _activeMousePlugInCollection.Element == null)
                        return;

                    InputReportEventArgs input = e.StagingItem.Input as InputReportEventArgs;

                    if (input.Report.Type != InputType.Mouse)
                        return;

                    RawMouseInputReport mouseInputReport = (RawMouseInputReport) input.Report;

                    if ((mouseInputReport.Actions & RawMouseActions.Deactivate) != RawMouseActions.Deactivate)
                        return;

                    mouseDevice = _inputManager.Value.PrimaryMouseDevice;

                    // Mouse set directly over to null when truly deactivating.
                    if (mouseDevice == null || mouseDevice.DirectlyOver != null)
                        return;

                    leftButtonDown = mouseDevice.LeftButton == MouseButtonState.Pressed;
                    rightButtonDown = mouseDevice.RightButton == MouseButtonState.Pressed;
                    timestamp = mouseInputReport.Timestamp;

                    // Get presentationsource from element.
                    source = PresentationSource.CriticalFromVisual(_activeMousePlugInCollection.Element as Visual);
                }
                else
                {
                    MouseEventArgs mouseEventArgs = e.StagingItem.Input as MouseEventArgs;
                    mouseDevice = mouseEventArgs.MouseDevice;
                    leftButtonDown = mouseDevice.LeftButton == MouseButtonState.Pressed;
                    rightButtonDown = mouseDevice.RightButton == MouseButtonState.Pressed;

                    // Only look at mouse input reports that truly come from a mouse (and is not an up or deactivate) and it
                    // must be pressed state if a move (we don't fire stylus inair moves currently)
                    if (mouseEventArgs.StylusDevice != null &&
                        e.StagingItem.Input.RoutedEvent != Mouse.PreviewMouseUpEvent)
                        return;

                    if (e.StagingItem.Input.RoutedEvent == Mouse.PreviewMouseMoveEvent)
                    {
                        if (!leftButtonDown)
                            return;
                        stylusActions = RawStylusActions.Move;
                    }
                    if (e.StagingItem.Input.RoutedEvent == Mouse.PreviewMouseDownEvent)
                    {
                        MouseButtonEventArgs mouseButtonEventArgs = mouseEventArgs as MouseButtonEventArgs;
                        if (mouseButtonEventArgs.ChangedButton != MouseButton.Left)
                            return;
                        stylusActions = RawStylusActions.Down;
                    }
                    if (e.StagingItem.Input.RoutedEvent == Mouse.PreviewMouseUpEvent)
                    {
                        MouseButtonEventArgs mouseButtonEventArgs = mouseEventArgs as MouseButtonEventArgs;
                        if (mouseButtonEventArgs.ChangedButton != MouseButton.Left)
                            return;
                        stylusActions = RawStylusActions.Up;
                    }
                    timestamp = mouseEventArgs.Timestamp;

                    Visual directlyOverVisual = mouseDevice.DirectlyOver as Visual;
                    if ( directlyOverVisual == null )
                    {
                        return;
                    }

                    // NTRAID:WINDOWSOS#1536432-2006/03/15-WAYNEZEN,
                    // Take the presentation source which is associated to the directly over element.
                    source = PresentationSource.CriticalFromVisual(directlyOverVisual);

                }

                PenContexts penContexts = GetPenContextsFromHwnd(source);

                if ((penContexts != null) &&
                    (source != null) &&
                    (source.CompositionTarget != null) &&
                    !source.CompositionTarget.IsDisposed)
                {
                    IInputElement directlyOver = mouseDevice.DirectlyOver;
                    int packetStatus = (leftButtonDown ? 1 : 0) | (rightButtonDown ? 9 : 0); // pen tip down == 1, barrel = 8
                    Point ptClient = mouseDevice.GetPosition(source.RootVisual as IInputElement);
                    ptClient  =  source.CompositionTarget.TransformToDevice.Transform(ptClient);

                    int buttons = (leftButtonDown ? 1 : 0) | (rightButtonDown ? 3 : 0);
                    int [] data = {(int)ptClient.X, (int)ptClient.Y, packetStatus, buttons};
                    RawStylusInputReport inputReport = new RawStylusInputReport(
                                                                InputMode.Foreground,
                                                                timestamp,
                                                                source,
                                                                stylusActions,
                                                                GetMousePointDescription,
                                                                data);

                    // Avoid re-entrancy due to lock() being used.
                    using (Dispatcher.DisableProcessing())
                    {
                        // Call plugins (does enter/leave and FireCustomData as well)
                        _activeMousePlugInCollection = penContexts.InvokeStylusPluginCollectionForMouse(inputReport, directlyOver, _activeMousePlugInCollection);
                    }
                }
            }
        }

        /////////////////////////////////////////////////////////////////////

        internal StylusPointDescription GetMousePointDescription
        {
            get
            {
                if (_mousePointDescription == null)
                {
                    _mousePointDescription = new StylusPointDescription(
                                                    new StylusPointPropertyInfo[] {
                                                        StylusPointPropertyInfoDefaults.X,
                                                        StylusPointPropertyInfoDefaults.Y,
                                                        StylusPointPropertyInfoDefaults.NormalPressure,
                                                        StylusPointPropertyInfoDefaults.PacketStatus,
                                                        StylusPointPropertyInfoDefaults.TipButton,
                                                        StylusPointPropertyInfoDefaults.BarrelButton
                                                    },
                                                    -1); // No real pressure in data
                }

                return _mousePointDescription;
            }
        }


        internal MouseButtonState GetMouseLeftOrRightButtonState(bool leftButton)
        {
            if (leftButton)
            {
                return _mouseLeftButtonState;
            }
            else
            {
                return _mouseRightButtonState;
            }
        }

        internal bool UpdateMouseButtonState(RawMouseActions actions)
        {
            bool updated = false;

            switch (actions)
            {
                case RawMouseActions.Button1Press:
                    if (_mouseLeftButtonState != MouseButtonState.Pressed)
                    {
                        updated = true;
                        _mouseLeftButtonState = MouseButtonState.Pressed;
                    }
                    break;

                case RawMouseActions.Button1Release:
                    if (_mouseLeftButtonState != MouseButtonState.Released)
                    {
                        updated = true;
                        _mouseLeftButtonState = MouseButtonState.Released;
                    }
                    break;

                case RawMouseActions.Button2Press:
                    if (_mouseRightButtonState != MouseButtonState.Pressed)
                    {
                        updated = true;
                        _mouseRightButtonState = MouseButtonState.Pressed;
                    }
                    break;

                case RawMouseActions.Button2Release:
                    if (_mouseRightButtonState != MouseButtonState.Released)
                    {
                        updated = true;
                        _mouseRightButtonState = MouseButtonState.Released;
                    }
                    break;
            }

            return updated;
        }

        /// <SecurityNote>
        ///      Critical as this calls a critical method MouseDevice.GetButtonStateFromSystem
        ///             and accesses Critical data _inputManager.Value.
        ///
        ///      At the top called from PreProcessInput and PreNotifyInput which is SecurityCritical
        /// </SecurityNote>
        [SecurityCritical]
        void UpdateMouseState()
        {
            MouseDevice mouseDevice = _inputManager.Value.PrimaryMouseDevice;
            _mouseLeftButtonState = mouseDevice.GetButtonStateFromSystem(MouseButton.Left);
            _mouseRightButtonState = mouseDevice.GetButtonStateFromSystem(MouseButton.Right);
        }


        // We walk the list of stylus devices looking to see if any of them are in range so we
        // can udpate the flag that tracks whether any stylus devices are currently in range.
        private void UpdateIsStylusInRange(bool forceInRange)
        {
            bool foundInRangeStylusDevice = false;

            if (forceInRange)
            {
                foundInRangeStylusDevice = true;
            }
            else
            {
                foreach (TabletDevice tabletDevice in Tablet.TabletDevices)
                {
                    foreach (StylusDevice stylusDevice in tabletDevice.StylusDevices)
                    {
                        if (stylusDevice.InRange)
                        {
                            foundInRangeStylusDevice = true;
                            break;
                        }
                    }

                    // Exit if we found a stylusdevice.
                    if (foundInRangeStylusDevice) break;
                }
            }

            // Update in range flag.
            _stylusDeviceInRange = foundInRangeStylusDevice;
        }


#if !MULTICAPTURE
        internal void UpdateStylusCapture(StylusDevice stylusDevice, IInputElement oldStylusDeviceCapture, IInputElement newStylusDeviceCapture, int timestamp)
        {
            if (newStylusDeviceCapture != _stylusCapture)
            {
                DependencyObject o = null;
                IInputElement oldCapture = _stylusCapture;
                _stylusCapture = newStylusDeviceCapture;

                // Adjust the handlers we use to track everything.
                if (oldCapture != null)
                {
                    o = oldCapture as DependencyObject;
                    if (InputElement.IsUIElement(o))
                    {
                        ((UIElement)o).IsEnabledChanged -= _captureIsEnabledChangedEventHandler;
                        ((UIElement)o).IsVisibleChanged -= _captureIsVisibleChangedEventHandler;
                        ((UIElement)o).IsHitTestVisibleChanged -= _captureIsHitTestVisibleChangedEventHandler;
                    }
                    else if (InputElement.IsContentElement(o))
                    {
                        ((ContentElement)o).IsEnabledChanged -= _captureIsEnabledChangedEventHandler;

                        // NOTE: there are no IsVisible or IsHitTestVisible properties for ContentElements.
                        //
                        // ((ContentElement)o).IsVisibleChanged -= _captureIsVisibleChangedEventHandler;
                        // ((ContentElement)o).IsHitTestVisibleChanged -= _captureIsHitTestVisibleChangedEventHandler;
                    }
                    else
                    {
                        ((UIElement3D)o).IsEnabledChanged -= _captureIsEnabledChangedEventHandler;
                        ((UIElement3D)o).IsVisibleChanged -= _captureIsVisibleChangedEventHandler;
                        ((UIElement3D)o).IsHitTestVisibleChanged -= _captureIsHitTestVisibleChangedEventHandler;
                    }
                }
                if (_stylusCapture != null)
                {
                    o = _stylusCapture as DependencyObject;
                    if (InputElement.IsUIElement(o))
                    {
                        ((UIElement)o).IsEnabledChanged += _captureIsEnabledChangedEventHandler;
                        ((UIElement)o).IsVisibleChanged += _captureIsVisibleChangedEventHandler;
                        ((UIElement)o).IsHitTestVisibleChanged += _captureIsHitTestVisibleChangedEventHandler;
                    }
                    else if (InputElement.IsContentElement(o))
                    {
                        ((ContentElement)o).IsEnabledChanged += _captureIsEnabledChangedEventHandler;

                        // NOTE: there are no IsVisible or IsHitTestVisible properties for ContentElements.
                        //
                        // ((ContentElement)o).IsVisibleChanged += _captureIsVisibleChangedEventHandler;
                        // ((ContentElement)o).IsHitTestVisibleChanged += _captureIsHitTestVisibleChangedEventHandler;
                    }
                    else
                    {
                        ((UIElement3D)o).IsEnabledChanged += _captureIsEnabledChangedEventHandler;
                        ((UIElement3D)o).IsVisibleChanged += _captureIsVisibleChangedEventHandler;
                        ((UIElement3D)o).IsHitTestVisibleChanged += _captureIsHitTestVisibleChangedEventHandler;
                    }
                }

                // Oddly enough, update the IsStylusCaptureWithin property first.  This is
                // so any callbacks will see the more-common IsStylusCaptureWithin property
                // set correctly.
                UIElement.StylusCaptureWithinProperty.OnOriginValueChanged(oldCapture as DependencyObject, _stylusCapture as DependencyObject, ref _stylusCaptureWithinTreeState);

                // Invalidate the IsStylusCaptured properties.
                if (oldCapture != null)
                {
                    o = oldCapture as DependencyObject;
                    o.SetValue(UIElement.IsStylusCapturedPropertyKey, false); // Same property for ContentElements
                }
                if (_stylusCapture != null)
                {
                    o = _stylusCapture as DependencyObject;
                    o.SetValue(UIElement.IsStylusCapturedPropertyKey, true); // Same property for ContentElements
                }
            }
        }

        internal void UpdateOverProperty(StylusDevice stylusDevice, IInputElement newOver)
        {
            // Only update the OverProperty for the current stylus device and only if we see a change.
            if (stylusDevice == _currentStylusDevice && newOver != _stylusOver)
            {
                DependencyObject o = null;
                IInputElement oldOver = _stylusOver;
                _stylusOver = newOver;

                // Adjust the handlers we use to track everything.
                if (oldOver != null)
                {
                    o = oldOver as DependencyObject;
                    if (InputElement.IsUIElement(o))
                    {
                        ((UIElement)o).IsEnabledChanged -= _overIsEnabledChangedEventHandler;
                        ((UIElement)o).IsVisibleChanged -= _overIsVisibleChangedEventHandler;
                        ((UIElement)o).IsHitTestVisibleChanged -= _overIsHitTestVisibleChangedEventHandler;
                    }
                    else if (InputElement.IsContentElement(o))
                    {
                        ((ContentElement)o).IsEnabledChanged -= _overIsEnabledChangedEventHandler;

                        // NOTE: there are no IsVisible or IsHitTestVisible properties for ContentElements.
                        //
                        // ((ContentElement)o).IsVisibleChanged -= _overIsVisibleChangedEventHandler;
                        // ((ContentElement)o).IsHitTestVisibleChanged -= _overIsHitTestVisibleChangedEventHandler;
                    }
                    else
                    {
                        ((UIElement3D)o).IsEnabledChanged -= _overIsEnabledChangedEventHandler;
                        ((UIElement3D)o).IsVisibleChanged -= _overIsVisibleChangedEventHandler;
                        ((UIElement3D)o).IsHitTestVisibleChanged -= _overIsHitTestVisibleChangedEventHandler;
                    }
                }
                if (_stylusOver != null)
                {
                    o = _stylusOver as DependencyObject;
                    if (InputElement.IsUIElement(o))
                    {
                        ((UIElement)o).IsEnabledChanged += _overIsEnabledChangedEventHandler;
                        ((UIElement)o).IsVisibleChanged += _overIsVisibleChangedEventHandler;
                        ((UIElement)o).IsHitTestVisibleChanged += _overIsHitTestVisibleChangedEventHandler;
                    }
                    else if (InputElement.IsContentElement(o))
                    {
                        ((ContentElement)o).IsEnabledChanged += _overIsEnabledChangedEventHandler;

                        // NOTE: there are no IsVisible or IsHitTestVisible properties for ContentElements.
                        //
                        // ((ContentElement)o).IsVisibleChanged += _overIsVisibleChangedEventHandler;
                        // ((ContentElement)o).IsHitTestVisibleChanged += _overIsHitTestVisibleChangedEventHandler;
                    }
                    else
                    {
                        ((UIElement3D)o).IsEnabledChanged += _overIsEnabledChangedEventHandler;
                        ((UIElement3D)o).IsVisibleChanged += _overIsVisibleChangedEventHandler;
                        ((UIElement3D)o).IsHitTestVisibleChanged += _overIsHitTestVisibleChangedEventHandler;
                    }
                }

                // Oddly enough, update the IsStylusOver property first.  This is
                // so any callbacks will see the more-common IsStylusOver property
                // set correctly.
                UIElement.StylusOverProperty.OnOriginValueChanged(oldOver as DependencyObject, _stylusOver as DependencyObject, ref _stylusOverTreeState);

                // Invalidate the IsStylusDirectlyOver property.
                if (oldOver != null)
                {
                    o = oldOver as DependencyObject;
                    o.SetValue(UIElement.IsStylusDirectlyOverPropertyKey, false); // Same property for ContentElements
                }
                if (_stylusOver != null)
                {
                    o = _stylusOver as DependencyObject;
                    o.SetValue(UIElement.IsStylusDirectlyOverPropertyKey, true); // Same property for ContentElements
                }
            }
        }


        private void OnOverIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is over just became disabled.
            //
            // We need to resynchronize the stylus so that we can figure out who
            // the stylus is over now.

            ReevaluateStylusOver(null, null, true);
        }

        private void OnOverIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is over just became non-visible (collapsed or hidden).
            //
            // We need to resynchronize the stylus so that we can figure out who
            // the stylus is over now.

            ReevaluateStylusOver(null, null, true);
        }

        private void OnOverIsHitTestVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is over was affected by a change in hit-test visibility.
            //
            // We need to resynchronize the stylus so that we can figure out who
            // the stylus is over now.

            ReevaluateStylusOver(null, null, true);
        }

        private void OnCaptureIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is captured to just became disabled.
            //
            // We need to re-evaluate the element that has stylus capture since
            // we can't allow the stylus to remain captured by a disabled element.

            ReevaluateCapture(null, null, true);
        }

        private void OnCaptureIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is captured to just became non-visible (collapsed or hidden).
            //
            // We need to re-evaluate the element that has stylus capture since
            // we can't allow the stylus to remain captured by a non-visible element.

            ReevaluateCapture(null, null, true);
        }

        private void OnCaptureIsHitTestVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The element that the stylus is captured to was affected by a change in hit-test visibility.
            //
            // We need to re-evaluate the element that has stylus capture since
            // we can't allow the stylus to remain captured by a non-hittest-visible element.

            ReevaluateCapture(null, null, true);
        }
#endif


        /// <summary>
        /// </summary>
        /// <SecurityNote>
        ///     Critical: Access the security critical method - StylusLogic.CurrentStylusLogic.
        ///     TreatAsSafe: Just calls security transparent routine ReevaluateStylusOver on StylusLogic.
        ///                  Takes no critical data as input and doesn't return any.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        static internal void CurrentStylusLogicReevaluateStylusOver(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
        {
            StylusLogic.CurrentStylusLogic.ReevaluateStylusOver(element, oldParent, isCoreParent);
        }

#if !MULTICAPTURE
        internal void ReevaluateStylusOver(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
#else
        private void ReevaluateStylusOver(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
#endif
        {
#if !MULTICAPTURE
            if (element != null)
            {
                if (isCoreParent)
                {
                    StylusOverTreeState.SetCoreParent(element, oldParent);
                }
                else
                {
                    StylusOverTreeState.SetLogicalParent(element, oldParent);
                }
            }

            // It would be best to re-evaluate anything dependent on the hit-test results
            // immediately after layout & rendering are complete.  Unfortunately this can
            // lead to an infinite loop.  Consider the following scenario:
            //
            // If the stylus is over an element, hide it.
            //
            // This never resolves to a "correct" state.  When the stylus moves over the
            // element, the element is hidden, so the stylus is no longer over it, so the
            // element is shown, but that means the stylus is over it again.  Repeat.
            //
            // We push our re-evaluation to a priority lower than input processing so that
            // the user can change the input device to avoid the infinite loops, or close
            // the app if nothing else works.
            //
            if (_reevaluateStylusOverOperation == null)
            {
                _reevaluateStylusOverOperation = Dispatcher.BeginInvoke(DispatcherPriority.Input, _reevaluateStylusOverDelegate, null);
            }
#else
            lock (__stylusDeviceLock)
            {
                foreach (var pair in __stylusDeviceMap)
                {
                    pair.Value.ReevaluateStylusOver(element, oldParent, isCoreParent);
                }
            }
#endif
        }

#if !MULTICAPTURE
        /// <summary>
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private object ReevaluateStylusOverAsync(object arg)
        {
            _reevaluateStylusOverOperation = null;

            // Synchronize causes state issues with the stylus events so we don't do this.
            //if (_currentStylusDevice != null)
            //{
            //    _currentStylusDevice.Synchronize();
            //}

            // Refresh StylusOverProperty so that ReverseInherited Flags are updated.
            //
            // We only need to do this is there is any information about the old
            // tree state.  This is because it is possible (even likely) that
            // Synchronize() would have already done this if we hit-tested to a
            // different element.
            if(_stylusOverTreeState != null && !_stylusOverTreeState.IsEmpty)
            {
                UIElement.StylusOverProperty.OnOriginValueChanged(_stylusOver as DependencyObject, _stylusOver as DependencyObject, ref _stylusOverTreeState);
            }

            return null;
        }
#endif

        /// <summary>
        /// </summary>
        /// <SecurityNote>
        ///     Critical: Access the security critical method StylusLogic.CurrentStylusLogic.
        ///     TreatAsSafe: Just calls security transparent routine ReevaluateCapture on StylusLogic.
        ///                  Takes no critical data as input and doesn't return any.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        static internal void CurrentStylusLogicReevaluateCapture(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
        {
            StylusLogic.CurrentStylusLogic.ReevaluateCapture(element, oldParent, isCoreParent);
        }

#if !MULTICAPTURE
        /// <summary>
        /// </summary>
        internal void ReevaluateCapture(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
#else
        private void ReevaluateCapture(DependencyObject element, DependencyObject oldParent, bool isCoreParent)
#endif
        {
#if !MULTICAPTURE
            if (element != null)
            {
                if (isCoreParent)
                {
                    StylusCaptureWithinTreeState.SetCoreParent(element, oldParent);
                }
                else
                {
                    StylusCaptureWithinTreeState.SetLogicalParent(element, oldParent);
                }
            }

            // We re-evaluate the captured element to be consistent with how
            // we re-evaluate the element the stylus is over.
            //
            // See ReevaluateStylusOver for details.
            //
            if (_reevaluateCaptureOperation == null)
            {
                _reevaluateCaptureOperation = Dispatcher.BeginInvoke(DispatcherPriority.Input, _reevaluateCaptureDelegate, null);
            }
#else
            lock (__stylusDeviceLock)
            {
                foreach (var pair in __stylusDeviceMap)
                {
                    pair.Value.ReevaluateCapture(element, oldParent, isCoreParent);
                }
            }
#endif
        }


#if !MULTICAPTURE
        /// <summary>
        ///
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private object ReevaluateCaptureAsync(object arg)
        {
            _reevaluateCaptureOperation = null;

            if (_stylusCapture == null)
                return null;

            bool killCapture = false;

            DependencyObject dependencyObject = _stylusCapture as DependencyObject;

            //
            // First, check things like IsEnabled, IsVisible, etc. on a
            // UIElement vs. ContentElement basis.
            //
            if (InputElement.IsUIElement(dependencyObject))
            {
                killCapture = !ValidateUIElementForCapture((UIElement)_stylusCapture);
            }
            else if (InputElement.IsContentElement(dependencyObject))
            {
                killCapture = !ValidateContentElementForCapture((ContentElement)_stylusCapture);
            }
            else
            {
                killCapture = !ValidateUIElement3DForCapture((UIElement3D)_stylusCapture);
            }

            //
            // Second, if we still haven't thought of a reason to kill capture, validate
            // it on a Visual basis for things like still being in the right tree.
            //
            if (killCapture == false)
            {
                DependencyObject containingVisual = InputElement.GetContainingVisual(dependencyObject);
                killCapture = !ValidateVisualForCapture(containingVisual);
            }

            //
            // Lastly, if we found any reason above, kill capture.
            //
            if (killCapture)
            {
                Stylus.Capture(null);
            }

            // Refresh StylusCaptureWithinProperty so that ReverseInherited flags are updated.
            //
            // We only need to do this is there is any information about the old
            // tree state.  This is because it is possible (even likely) that
            // we would have already killed capture if the capture criteria was
            // no longer met.
            if (_stylusCaptureWithinTreeState != null && !_stylusCaptureWithinTreeState.IsEmpty)
            {
                UIElement.StylusCaptureWithinProperty.OnOriginValueChanged(_stylusCapture as DependencyObject, _stylusCapture as DependencyObject, ref _stylusCaptureWithinTreeState);
            }

            return null;
        }

        private bool ValidateUIElementForCapture(UIElement element)
        {
            if (element.IsEnabled == false)
                return false;

            if (element.IsVisible == false)
                return false;

            if (element.IsHitTestVisible == false)
                return false;

            return true;
        }

        private bool ValidateContentElementForCapture(ContentElement element)
        {
            if (element.IsEnabled == false)
                return false;

            // NOTE: there is no IsVisible property for ContentElements.

            return true;
        }

        private bool ValidateUIElement3DForCapture(UIElement3D element)
        {
            if (element.IsEnabled == false)
                return false;

            if (element.IsVisible == false)
                return false;

            if (element.IsHitTestVisible == false)
                return false;

            return true;
        }

        /// <SecurityNote>
        ///     Critical: This code accesses critical data (PresentationSource)
        ///     TreatAsSafe: Although it accesses critical data it does not modify or expose it, only compares against it.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        private bool ValidateVisualForCapture(DependencyObject visual)
        {
            if (visual == null)
                return false;

            PresentationSource presentationSource = PresentationSource.CriticalFromVisual(visual);

            if (presentationSource == null)
            {
                return false;
            }

            if (CurrentStylusDevice != null &&
                CurrentStylusDevice.CriticalActiveSource != presentationSource &&
                CurrentStylusDevice.Captured == null)
            {
                return false;
            }

            return true;
        }
#endif


        /////////////////////////////////////////////////////////////////////

        // Make sure that the state of the stylus is correct for the the event we are
        // seeing.  This validation is mainly for V1 and Lonestar wisptis events since
        // it can send us InAirMove while in the middle of a down state.
        // The other issue this routine handles is overlapping InRange and OutOfRange
        // notifications we can get when moving between two windows (penContexts).  Wisptis
        // can send these in an overlapped manner which can mess up our InRange state if
        // we don't special case it.
        /// <SecurityNote>
        ///     Critical: - asserts for UIPermission to grab the active source from the stylusdevice
        ///               - accesses SecurityCritical data _inputManager.Value.
        ///               - calls SecurityCritical methods (GetStylusPenContextForHwnd, StylusDevice.ActiveSource,
        ///                     HwndSource.CriticalHandle, InputReport.InputSource and InputManager.ProcessInput).
        ///           - called by PreProcessInput
        ///           - Not TAS since this can inject stylus events.
        /// </SecurityNote>
        [SecurityCritical]
        bool IsValidStylusAction(RawStylusInputReport rawStylusInputReport)
        {
            bool allowEvent = true;
            StylusDevice stylusDevice = rawStylusInputReport.StylusDevice;

            // See if we have the correct PenContext we are receiving input from.  We can get two
            // different PenContext objects actually sending us input simultaneously.  We lock onto the
            // the first one we see come in range and keep locked on that till we see it go out of range.
            // If we go out of range and have overlapping inrange from another PenContext we will
            // force that PenContext to be the current one by forcing a InRange stylus event.

            // Now check for proper state of the device for the given Stylus event.
            switch (rawStylusInputReport.Actions)
            {
                case RawStylusActions.InRange:
                    // only process inrange if currently out of range and the inputsource is not disposed.
                    allowEvent = !stylusDevice.InRange && !rawStylusInputReport.InputSource.IsDisposed;
                    break;

                case RawStylusActions.InAirMove:
                    if (!stylusDevice.InRange && !rawStylusInputReport.InputSource.IsDisposed)
                    {
                        // Force InRange if stylus is out of range.
                        Debug.Assert(stylusDevice.InAir);
                        GenerateInRange(rawStylusInputReport);
                    }
                    else
                    {
                        // If InAir and either inputSource matches current devices input source or
                        // the last down input source then it is OK to process and we can allow this.
                        allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;
                    }
                    break;

                case RawStylusActions.Down:
                    if (!stylusDevice.InRange)
                    {
                        Debug.Assert(stylusDevice.InAir);
                        GenerateInRange(rawStylusInputReport);
                    }
                    else
                    {
                        allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;
                    }
                    break;

                case RawStylusActions.Move:
                    allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;
                    break;

                case RawStylusActions.Up:
                    allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;
                    break;

                case RawStylusActions.SystemGesture:
                    allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;

                    if (allowEvent)
                    {
                        RawStylusSystemGestureInputReport systemGestureReport = (RawStylusSystemGestureInputReport)rawStylusInputReport;

                        // If we see a Tap gesture that is sent when we are not in the down state then
                        // ignore this (it's a double tap issue) since we will have generated one when
                        // we see the up.
                        if (systemGestureReport.SystemGesture == SystemGesture.Tap && stylusDevice.InAir)
                        {
                            allowEvent = false;
                        }
                    }
                    break;

                case RawStylusActions.OutOfRange:
                    allowEvent = rawStylusInputReport.PenContext == stylusDevice.ActivePenContext;
                    break;
            }

            return allowEvent;
        }


        /// <SecurityNote>
        ///     Critical: - accesses SecurityCritical data (_inputManager.Value, rawStylusInputReport.Data,
        ///                     InputReport.InputSource, rawStylusInputReport.PenContext).
        ///               - calls SecurityCritical methods (InputManager.ProcessInput).
        ///           - Not TAS since this can inject stylus events.
        /// </SecurityNote>
        [SecurityCritical]
        private void GenerateInRange(RawStylusInputReport rawStylusInputReport)
        {
            StylusDevice stylusDevice = rawStylusInputReport.StylusDevice;

            RawStylusInputReport inputReport =
                new RawStylusInputReport(rawStylusInputReport.Mode,
                                         rawStylusInputReport.Timestamp,
                                         rawStylusInputReport.InputSource,
                                         rawStylusInputReport.PenContext,
                                         RawStylusActions.InRange,
                                         stylusDevice.TabletDevice.Id,
                                         stylusDevice.Id,
                                         rawStylusInputReport.Data);

            InputReportEventArgs input = new InputReportEventArgs(stylusDevice, inputReport);
            input.RoutedEvent=InputManager.PreviewInputReportEvent;
            _inputManager.Value.ProcessInput(input);
        }

        /////////////////////////////////////////////////////////////////////

        internal static int GetFlickAction(int flickData)
        {
            return (flickData & 0x001F);
        }

        private static bool GetIsScrollUp(int flickData)
        {
            Debug.Assert(GetFlickAction(flickData) == 1); // Make sure scroll action is set in flickdata.
            return (NativeMethods.SignedHIWORD(flickData) == 0);
        }

        // Our handler for the WM_FLICK message.
        internal bool HandleFlick(int flickData, IInputElement element)
        {
            bool handled = false; // By default say we didn't handle this flick action.

            switch (GetFlickAction(flickData))
            {
                case 1: // Scroll
                    // Process scroll command.
                    RoutedUICommand command = GetIsScrollUp(flickData) ? ComponentCommands.ScrollPageUp : ComponentCommands.ScrollPageDown;
                    if (element != null)
                    {
                        if (command.CanExecute(null, element))
                        {
                            command.Execute(null, element);
                        }

                        // NTRAID-WINDOWS#1412363-2006/01/10-WAYNEZEN,
                        // We should alway report handled if there is a potential flick element.
                        // Otherwise, the flick UIHub will send WM_KEYXXX for the PageDown/PageUp to this window.
                        // So the focused element will react to those messages.
                        handled = true; // Say we handled it.
                    }
                    break;

                case 0: // Null
                case 2: // AppCommand
                case 3: // CustomKey
                case 4: // KeyModifier
                    // Say we didn't handle it so UIHub will do default processing.
                    break;

                default:
                    Debug.Assert(false, "Unknown Flick Action encountered");
                    break;
            }

            return handled;
        }


        /// <summary>
        /// This method handles the various windows messages related to the system setting changes.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <SecurityNote>
        ///     Critical - Calls into SecurityCritical code (OnDeviceChange, OnScreenMeasurementsChanged,
        ///                 ReadSystemConfig, OnTabletAdded and OnTabletRemoved).
        /// </SecurityNote>
        [SecurityCritical, FriendAccessAllowed]
        internal void HandleMessage(WindowMessage msg, IntPtr wParam, IntPtr lParam)
        {
            switch ( msg )
            {
                case WindowMessage.WM_DEVICECHANGE:
                    if ( !_inputEnabled && (uint)NativeMethods.IntPtrToInt32(wParam) == 0x0007 /* DBT_DEVNODES_CHANGED */)
                    {
                        OnDeviceChange();
                    }
                    break;

                case WindowMessage.WM_DISPLAYCHANGE:
                    OnScreenMeasurementsChanged();
                    break;

                case WindowMessage.WM_SETTINGCHANGE:
                    ReadSystemConfig(); // Update our registry settings.

                    // Invalidate the values so they get re-built as needed.
                    if (_tabletDeviceCollection != null)
                    {
                        foreach (TabletDevice tablet in _tabletDeviceCollection)
                        {
                            tablet.InvalidateSizeDeltas(); // Will be recalc'd on next stylus down.
                        }
                    }
                    break;

                case WindowMessage.WM_TABLET_ADDED:
                    OnTabletAdded((uint)NativeMethods.IntPtrToInt32(wParam));
                    break;

                case WindowMessage.WM_TABLET_DELETED:
                    OnTabletRemoved((uint)NativeMethods.IntPtrToInt32(wParam));
                    break;
            }
        }

        /////////////////////////////////////////////////////////////////////

        RoutedEvent GetMainEventFromPreviewEvent(RoutedEvent routedEvent)
        {
            if (routedEvent == Stylus.PreviewStylusDownEvent)
                return Stylus.StylusDownEvent;
            if (routedEvent == Stylus.PreviewStylusUpEvent)
                return Stylus.StylusUpEvent;
            if (routedEvent == Stylus.PreviewStylusMoveEvent)
                return Stylus.StylusMoveEvent;
            if (routedEvent == Stylus.PreviewStylusInAirMoveEvent)
                return Stylus.StylusInAirMoveEvent;
            if (routedEvent == Stylus.PreviewStylusInRangeEvent)
                return Stylus.StylusInRangeEvent;
            if (routedEvent == Stylus.PreviewStylusOutOfRangeEvent)
                return Stylus.StylusOutOfRangeEvent;
            if (routedEvent == Stylus.PreviewStylusSystemGestureEvent)
                return Stylus.StylusSystemGestureEvent;
            if (routedEvent == Stylus.PreviewStylusButtonDownEvent)
                return Stylus.StylusButtonDownEvent;
            if (routedEvent == Stylus.PreviewStylusButtonUpEvent)
                return Stylus.StylusButtonUpEvent;
            return null;
        }

        /////////////////////////////////////////////////////////////////////

        RoutedEvent GetPreviewEventFromRawStylusActions(RawStylusActions actions)
        {
            if((actions & RawStylusActions.Down) != 0)
                return Stylus.PreviewStylusDownEvent;
            if ((actions & RawStylusActions.Up) != 0)
                return Stylus.PreviewStylusUpEvent;
            if ((actions & RawStylusActions.Move) != 0)
                return Stylus.PreviewStylusMoveEvent;
            if ((actions & RawStylusActions.InAirMove) != 0)
                return Stylus.PreviewStylusInAirMoveEvent;
            if ((actions & RawStylusActions.InRange) != 0)
                return Stylus.PreviewStylusInRangeEvent;
            if ((actions & RawStylusActions.OutOfRange) != 0)
                return Stylus.PreviewStylusOutOfRangeEvent;
            if ((actions & RawStylusActions.SystemGesture) != 0)
                return Stylus.PreviewStylusSystemGestureEvent;
            return null;
        }

        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        /// Critical - Calls into SecurityCritical code.
        ///           - called by SecurityCritical code (Event handlers at top)
        /// </SecurityNote>
        [SecurityCritical]
        internal void InvokeStylusPluginCollection(RawStylusInputReport inputReport)
        {
            if (inputReport.StylusDevice != null)
            {
                inputReport.PenContext.Contexts.InvokeStylusPluginCollection(inputReport);
            }
        }

        /// <SecurityNote>
        ///     Critical - Calls in to SecurityCritical code (StylusDevice.UpdateEventStylusPoints)
        ///      At the top called from StylusLogic::PreNotifyInput event which is SecurityCritical.
        /// </SecurityNote>
        [SecurityCritical]
        private void VerifyStylusPlugInCollectionTarget(RawStylusInputReport rawStylusInputReport)
        {
            switch (rawStylusInputReport.Actions)
            {
                case RawStylusActions.Down:
                case RawStylusActions.Move:
                case RawStylusActions.Up:
                    break;
                default:
                    return; // do nothing if not Down, Move or Up.
            }

            RawStylusInput originalRSI = rawStylusInputReport.RawStylusInput;
            // See if we have a plugin for the target of this input.
            StylusPlugInCollection targetPIC = null;
            StylusPlugInCollection targetRtiPIC = (originalRSI != null) ? originalRSI.Target : null;
            bool updateEventPoints = false;

            // Make sure we use UIElement for target if non NULL and hit ContentElement.
            UIElement newTarget = InputElement.GetContainingUIElement(rawStylusInputReport.StylusDevice.DirectlyOver as DependencyObject) as UIElement;
            if (newTarget != null)
            {
                targetPIC = rawStylusInputReport.PenContext.Contexts.FindPlugInCollection(newTarget);
            }

            // Make sure any lock() calls do not reenter on us.
            using(Dispatcher.DisableProcessing())
            {
                // See if we hit the wrong PlugInCollection on the pen thread and clean things up if we did.
                if (targetRtiPIC != null && targetRtiPIC != targetPIC && originalRSI != null)
                {
                    // Fire custom data not confirmed events for both pre and post since bad target...
                    foreach (RawStylusInputCustomData customData in originalRSI.CustomDataList)
                    {
                        customData.Owner.FireCustomData(customData.Data, rawStylusInputReport.Actions, false);
                    }

                    updateEventPoints = originalRSI.StylusPointsModified;

                    // Clear RawStylusInput data.
                    rawStylusInputReport.RawStylusInput = null;
                }

                // See if we need to build up an RSI to send to the plugincollection (due to a mistarget).
                bool sendRawStylusInput = false;
                if (targetPIC != null && rawStylusInputReport.RawStylusInput == null)
                {
                    // NOTE: PenContext info will not change (it gets rebuilt instead so keeping ref is fine)
                    //    The transformTabletToView matrix and plugincollection rects though can change based
                    //    off of layout events which is why we need to lock this.
                    GeneralTransformGroup transformTabletToView = new GeneralTransformGroup();
                    transformTabletToView.Children.Add(new MatrixTransform(GetTabletToViewTransform(rawStylusInputReport.StylusDevice.TabletDevice))); // this gives matrix in measured units (not device)
                    transformTabletToView.Children.Add(targetPIC.ViewToElement); // Make it relative to the element.
                    transformTabletToView.Freeze();  // Must be frozen for multi-threaded access.

                    RawStylusInput rawStylusInput = new RawStylusInput(rawStylusInputReport, transformTabletToView, targetPIC);
                    rawStylusInputReport.RawStylusInput = rawStylusInput;
                    sendRawStylusInput = true;
                }

                // Now fire the confirmed enter/leave events as necessary.
                StylusPlugInCollection currentTarget = rawStylusInputReport.StylusDevice.CurrentVerifiedTarget;
                if (targetPIC != currentTarget)
                {
                    if (currentTarget != null)
                    {
                        // Fire leave event.  If we never had a plugin for this event then create a temp one.
                        if (originalRSI == null)
                        {
                            GeneralTransformGroup transformTabletToView = new GeneralTransformGroup();
                            transformTabletToView.Children.Add(new MatrixTransform(GetTabletToViewTransform(rawStylusInputReport.StylusDevice.TabletDevice))); // this gives matrix in measured units (not device)
                            transformTabletToView.Children.Add(currentTarget.ViewToElement); // Make it relative to the element.
                            transformTabletToView.Freeze();  // Must be frozen for multi-threaded access.
                            originalRSI = new RawStylusInput(rawStylusInputReport, transformTabletToView, currentTarget);
                        }
                        currentTarget.FireEnterLeave(false, originalRSI, true);
                    }

                    if (targetPIC != null)
                    {
                        // Fire Enter event
                        targetPIC.FireEnterLeave(true, rawStylusInputReport.RawStylusInput, true);
                    }

                    // Update the verified target.
                    rawStylusInputReport.StylusDevice.CurrentVerifiedTarget = targetPIC;
                }


                // Now fire RawStylusInput if needed to the right plugincollection.
                if (sendRawStylusInput)
                {
                    // We are on the pen thread, just call directly.
                    targetPIC.FireRawStylusInput(rawStylusInputReport.RawStylusInput);
                    updateEventPoints = (updateEventPoints || rawStylusInputReport.RawStylusInput.StylusPointsModified);
                }

                // Now fire PrePreviewCustomData events.
                if (targetPIC != null)
                {
                    // Send custom data pre event
                    foreach (RawStylusInputCustomData customData in rawStylusInputReport.RawStylusInput.CustomDataList)
                    {
                        customData.Owner.FireCustomData(customData.Data, rawStylusInputReport.Actions, true);
                    }
                }

                // VerifyRawTarget might resend to correct plugins or may have hit the wrong plugincollection.  The StylusPackets
                // may be overriden in those plugins so we need to call UpdateEventStylusPoints to update things.
                if (updateEventPoints)
                {
                    rawStylusInputReport.StylusDevice.UpdateEventStylusPoints(rawStylusInputReport, true);
                }
            }
        }

        /////////////////////////////////////////////////////////////////////


        internal int DoubleTapDelta
        {
            get {return _doubleTapDelta;}
        }

        internal int DoubleTapDeltaTime
        {
            get {return _doubleTapDeltaTime;}
        }

        internal int CancelDelta
        {
            get {return _cancelDelta;}
        }

        /// <SecurityNote>
        /// Critical - Calls into security critical code. (InputManagerProcessInputEventArgs)
        ///           - called by PreProcessInput (which is SecurityCritical)
        /// </SecurityNote>
        [SecurityCritical]
        private void GenerateGesture(RawStylusInputReport rawStylusInputReport, SystemGesture gesture)
        {
            StylusDevice stylusDevice = rawStylusInputReport.StylusDevice;
            System.Diagnostics.Debug.Assert(stylusDevice != null);

            RawStylusSystemGestureInputReport inputReport = new RawStylusSystemGestureInputReport(
                                                        InputMode.Foreground,
                                                        rawStylusInputReport.Timestamp,
                                                        rawStylusInputReport.InputSource,
                                                        rawStylusInputReport.PenContext,
                                                        rawStylusInputReport.TabletDeviceId,
                                                        rawStylusInputReport.StylusDeviceId,
                                                        gesture,
                                                        0, // Gesture X location (only used for flicks)
                                                        0, // Gesture Y location (only used for flicks)
                                                        0); // ButtonState (only used for flicks)
            inputReport.StylusDevice = stylusDevice;
            InputReportEventArgs input = new InputReportEventArgs(stylusDevice, inputReport);
            input.RoutedEvent=InputManager.PreviewInputReportEvent;
            // Process this directly instead of doing a push. We want this event to get
            // to the user before the StylusUp and MouseUp event.
            InputManagerProcessInputEventArgs(input);
        }

        /// Before we promote a MouseUp, we need to send a MouseMove to move the mouse device
        /// over to the correct location, since MouseUp does not use location information.
        /// <SecurityNote>
        /// Critical - Calls into security critical code. (InputManagerProcessInputEventArgs)
        ///           - called by PreProcessInput (which is SecurityCritical)
        /// </SecurityNote>
        [SecurityCritical]
        private void ProcessMouseMove(StylusDevice stylusDevice, int timestamp, bool isSynchronize)
        {
            System.Diagnostics.Debug.Assert(stylusDevice != null);

            if (!ShouldPromoteToMouse(stylusDevice) || !stylusDevice.TouchDevice.PromotingToOther)
            {
                return;
            }

            PresentationSource mouseInputSource = stylusDevice.GetMousePresentationSource();

            if (mouseInputSource != null)
            {
                RawMouseActions actions = RawMouseActions.AbsoluteMove;

                // Don't set Activate flag if a synchronize is requested!
                if (!isSynchronize)
                {
                    if (_inputManager.Value.PrimaryMouseDevice.CriticalActiveSource != mouseInputSource)
                    {
                        actions |= RawMouseActions.Activate;
                    }
                }

                Point pt = stylusDevice.LastMouseScreenPoint; // Use last promoted mouse location.
                pt = PointUtil.ScreenToClient(pt, mouseInputSource);

                RawMouseInputReport mouseInputReport =
                            new RawMouseInputReport(InputMode.Foreground,
                                                    timestamp,
                                                    mouseInputSource,
                                                    actions,
                                                    (int)pt.X,
                                                    (int)pt.Y,
                                                    0,
                                                    IntPtr.Zero);

                if (isSynchronize)
                {
                    mouseInputReport._isSynchronize = true;
                }

                _lastRawMouseAction = actions;

                InputReportEventArgs inputReportArgs = new InputReportEventArgs(stylusDevice, mouseInputReport);
                inputReportArgs.RoutedEvent = InputManager.PreviewInputReportEvent;

                // Process this directly instead of doing a push. We want this event to get
                // to the user before the StylusUp and MouseUp event.
                InputManagerProcessInputEventArgs(inputReportArgs);
            }
        }


        /////////////////////////////////////////////////////////////////////
        ///<SecurityNote>
        /// Critical: accesses e.StagingItem.Input
        ///             calls into SecurityCritical code (InputManagerProcessInputEventArgs)
        ///</SecurityNote>
        [SecurityCritical]
        private void UpdateButtonStates(ProcessInputEventArgs e)
        {
            if (!e.StagingItem.Input.Handled)
            {
                RoutedEvent routedEvent = e.StagingItem.Input.RoutedEvent;
                if (routedEvent != null && (routedEvent == Stylus.StylusDownEvent
                                     || routedEvent == Stylus.StylusUpEvent
                                     || routedEvent == Stylus.StylusMoveEvent
                                     || routedEvent == Stylus.StylusInAirMoveEvent))
                {
                    StylusEventArgs eventArgs = (StylusEventArgs)e.StagingItem.Input;
                    RawStylusInputReport report = eventArgs.InputReport;

                    StylusDevice stylusDevice = report.StylusDevice;
                    System.Diagnostics.Debug.Assert(stylusDevice != null);

                    StylusPointCollection stylusPoints = stylusDevice.GetStylusPoints(null);
                    StylusPoint stylusPoint = stylusPoints[stylusPoints.Count - 1];

                    foreach (StylusButton button in stylusDevice.StylusButtons)
                    {

                        // ISSUE-2005/2/2-XIAOTU what if more than one button state in a single packet?
                        // Split the packets or only use the last one as we did below?

                        StylusButtonState currentButtonState =
                            (StylusButtonState)stylusPoint.GetPropertyValue(new StylusPointProperty(button.Guid, true));

                        if (currentButtonState != button.CachedButtonState)
                        {
                           button.CachedButtonState = currentButtonState;

                           // do work to push Button event
                           StylusButtonEventArgs args = new StylusButtonEventArgs(stylusDevice, report.Timestamp, button);
                           args.InputReport = report;
                           if (currentButtonState == StylusButtonState.Down)
                           {
                               args.RoutedEvent=Stylus.PreviewStylusButtonDownEvent;
                           }
                           else
                           {
                               args.RoutedEvent=Stylus.PreviewStylusButtonUpEvent;
                           }

                           // Process this directly instead of doing a push. We want this event to get
                           // to the user before the promoted mouse event.
                           InputManagerProcessInputEventArgs(args);
                        }
                    }
                }
            }
        }


        ///<SecurityNote>
        ///     Critical:    calls UnsafeNativeMethods.WindowFromPoint
        ///</SecurityNote>
        [SecurityCritical]
        private static bool InWindowClientRect(Point ptClient, PresentationSource inputSource)
        {
            bool inClientRect = false;

            // Note: this only works for HWNDs for now.
            HwndSource source = inputSource as HwndSource;
            if(source != null && source.CompositionTarget != null && !source.IsHandleNull)
            {
                Point ptScreen = PointUtil.ClientToScreen(ptClient, source);
                IntPtr hwndHit = IntPtr.Zero ;
                HwndSource sourceHit = null ;
                Point ptClientHit = new Point(0,0);

                // Hit-test for a window.
                // See if this is one of our windows.
                hwndHit = UnsafeNativeMethods.WindowFromPoint((int)ptScreen.X, (int)ptScreen.Y);

                if(hwndHit != IntPtr.Zero)
                {
                    // See if this is one of our windows.
                    sourceHit = HwndSource.CriticalFromHwnd(hwndHit);

                    // We need to check if the point is over the client or
                    // non-client area.  We only care about being over the
                    // client area.
                    if (sourceHit != null)
                    {
                        ptClientHit = PointUtil.ScreenToClient(ptScreen, sourceHit);
                        NativeMethods.RECT rcClient = new NativeMethods.RECT();
                        SafeNativeMethods.GetClientRect(new HandleRef(sourceHit,hwndHit), ref rcClient);

                        // Don't consider we hit anything if we are over the non-client area.
                        inClientRect = !((int)ptClientHit.X < rcClient.left ||
                                         (int)ptClientHit.X >= rcClient.right ||
                                         (int)ptClientHit.Y < rcClient.top ||
                                         (int)ptClientHit.Y >= rcClient.bottom);
                    }
                }
            }
            return inClientRect;
        }

        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: calls into SecurityCritical code (TabletDeviceCollection constructor)
        /// </SecurityNote>
        internal TabletDeviceCollection TabletDevices
        {
            [SecurityCritical]
            get
            {
                if (_tabletDeviceCollection == null)
                {
                    _tabletDeviceCollection = new TabletDeviceCollection();

                    // We need to know when the dispatcher shuts down in order to clean
                    // up references to PenThreads held in the TabletDeviceCollection.
                    _inputManager.Value.Dispatcher.ShutdownFinished += _shutdownHandler;
                }
                return _tabletDeviceCollection;
            }
        }

        /////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     [TBS]
        /// </summary>
        internal StylusDevice CurrentStylusDevice
        {
            get
            {
                return _currentStylusDevice;
            }
        }

        /////////////////////////////////////////////////////////////////////

        internal void RegisterStylusDeviceCore(StylusDevice stylusDevice)
        {
            lock (__stylusDeviceLock)
            {
                int stylusDeviceId = stylusDevice.Id;
                // The map must contain unique entries for each stylus device.
                if (__stylusDeviceMap.ContainsKey(stylusDeviceId))
                {
                    InvalidOperationException ioe = new InvalidOperationException();
                    // We add a tag here so we can check for this specific exception
                    // in TabletCollection when adding new tablet devices.
                    ioe.Data.Add("System.Windows.Input.StylusLogic", "");
                    throw(ioe);
                }
                __stylusDeviceMap[stylusDeviceId] = stylusDevice;
            }
        }

        /////////////////////////////////////////////////////////////////////

        internal void UnregisterStylusDeviceCore(StylusDevice stylusDevice)
        {
            lock (__stylusDeviceLock)
            {
                Debug.Assert(__stylusDeviceMap.ContainsKey(stylusDevice.Id));
                __stylusDeviceMap.Remove(stylusDevice.Id);
            }
        }

        /////////////////////////////////////////////////////////////////////

        internal StylusDevice FindStylusDevice(int stylusDeviceId)
        {
            // If not on stylusLogic thread then you must take __stylusDeviceLock!!
            Debug.Assert(Dispatcher.CheckAccess());
            StylusDevice stylusDevice;
            __stylusDeviceMap.TryGetValue(stylusDeviceId, out stylusDevice);
            return stylusDevice;
        }

        internal StylusDevice FindStylusDeviceWithLock(int stylusDeviceId)
        {
            StylusDevice stylusDevice;
            lock (__stylusDeviceLock)
            {
                __stylusDeviceMap.TryGetValue(stylusDeviceId, out stylusDevice);
            }
            return stylusDevice;
        }

        /////////////////////////////////////////////////////////////////////


        // Updates the currently active stylus device and makes sure the StylusOver
        // property is updated as needed.
        internal void SelectStylusDevice(StylusDevice stylusDevice, IInputElement newOver, bool updateOver)
        {
            bool stylusDeviceChange = (_currentStylusDevice != stylusDevice);
            StylusDevice oldStylusDevice = _currentStylusDevice;

            // If current StylusDevice is becoming null, make sure we update the over state
            // before we update _currentStylusDevice or else the over property will not update
            // correctly!
#if !MULTICAPTURE
            if (updateOver && stylusDevice == null && stylusDeviceChange)
#else
            if (updateOver && stylusDevice == null && stylusDeviceChange && newOver == null)
#endif
            {
                // This will cause UpdateOverProperty() to be called.
                _currentStylusDevice.ChangeStylusOver(newOver); // This should be null.
            }

            _currentStylusDevice = stylusDevice;

            if (updateOver && stylusDevice != null)
            {
                // This will cause StylusLogic.UpdateStylusOverProperty to unconditionally be called.
                stylusDevice.ChangeStylusOver(newOver);

                // If changing the current stylusdevice make sure that the old one's
                // over state is set to null if it is not InRange anymore.
                // NOTE: We only want to do this if we have multiple stylusdevices InRange!
                if (stylusDeviceChange && oldStylusDevice != null)
                {
                    if (!oldStylusDevice.InRange)
                    {
                        oldStylusDevice.ChangeStylusOver(null);
                    }
                }
            }
        }

        /////////////////////////////////////////////////////////////////////
        /// <SecurityNote>
        /// Critical - calls into SecurityCritical code (Enable)
        /// </SecurityNote>
        [SecurityCritical]
        internal void EnableCore()
        {
            lock (__penContextsLock)
            {
                foreach (PenContexts contexts in __penContextsMap.Values)
                {
                    contexts.Enable();
                }
                _inputEnabled = true;
            }
        }

        /////////////////////////////////////////////////////////////////////

        internal bool Enabled
        {
            get
            {
                return _inputEnabled;
            }
        }

        /////////////////////////////////////////////////////////////////////
        /// <SecurityNote>
        ///     Critical: This code accesses critical data (hwnd, InputManager, _penContextsMap,
        ///                 PenContexts constructor) and passes to PenContexts constructor.
        ///               It also optionally calls critical method PenContexts.Enable().
        ///
        ///                - TreatAsSafe boundry at HwndSource constructor.
        /// </SecurityNote>
        [SecurityCritical]
        internal void RegisterHwndForInput(InputManager inputManager, PresentationSource inputSource)
        {
            HwndSource hwndSource = (HwndSource)inputSource;

            // Query the transform from HwndTarget when the first window is created.
            if (!_transformInitialized)
            {
                if (hwndSource != null && hwndSource.CompositionTarget != null)
                {
                    _transformToDevice = hwndSource.CompositionTarget.TransformToDevice;
                    Debug.Assert(_transformToDevice.HasInverse);
                    _transformInitialized = true;
                }
            }

            // Keep track so we don't bother looking for changes if someone happened to query this before
            // an Avalon window was created where we get TabletAdd/Removed notification.
            bool initializedTablets = (_tabletDeviceCollection == null);

            // This causes EnableCore to be called on TabletPC systems which enabled stylus input!
            TabletDeviceCollection tablets = TabletDevices;

            lock (__penContextsLock)
            {
                if (__penContextsMap.ContainsKey(inputSource))
                {
                    throw new InvalidOperationException(SR.Get(SRID.PenService_WindowAlreadyRegistered));
                }

                PenContexts penContexts = new PenContexts(inputManager.StylusLogic, inputSource);

                __penContextsMap[inputSource] = penContexts;

                // If FIRST one set this as the one to manage TabletAdded/Removed notifications.
                if (__penContextsMap.Count == 1)
                {
                    // Make sure our view of TabletDevices is up to date we didn't just cause it
                    // to be initialized and we had some real tablet devices.
                    if (!initializedTablets && tablets.Count > 0)
                    {
                        tablets.UpdateTablets();
                    }
                }

                // Detect if this window is disabled. If so then let the pencontexts know.
                int style = UnsafeNativeMethods.GetWindowLong(new HandleRef(this,hwndSource.CriticalHandle), NativeMethods.GWL_STYLE);
                if ((style & NativeMethods.WS_DISABLED) != 0)
                {
                    penContexts.IsWindowDisabled = true;
                }

                if ( _inputEnabled )
                    penContexts.Enable();
            }
        }

        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: - calls into Security Critical code (PenContexts.Disable)
        ///         and accesses critical member __penContextsMap
        ///
        ///                - TreatAsSafe boundry at HwndStylusInputProvider::Dispose
        /// </SecurityNote>
        [SecurityCritical]
        internal void UnRegisterHwndForInput(HwndSource hwndSource)
        {
            bool shutdownWorkThread = Dispatcher.HasShutdownStarted;
            lock (__penContextsLock)
            {
                PenContexts penContexts;
                if (__penContextsMap.TryGetValue(hwndSource, out penContexts))
                {
                    __penContextsMap.Remove(hwndSource);

                    // NTRAID:WINDOWSOS#1877251-2006/10/10-WAYNEZEN,
                    // If the application dispatcher is being shut down, we should destroy our pen thread as well.
                    penContexts.Disable(shutdownWorkThread);

                    // Make sure we remember the last location of this window for mapping stylus input later.
                    if (UnsafeNativeMethods.IsWindow(new HandleRef(hwndSource, hwndSource.CriticalHandle)))
                    {
                        penContexts.DestroyedLocation = PointUtil.ClientToScreen(new Point(0, 0), hwndSource);
                    }
                }

                // If we failed to find penContexts for this window above then throw an error now.
                if (penContexts == null)
                {
                    throw new InvalidOperationException(SR.Get(SRID.PenService_WindowNotRegistered));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: - Accesses a critical member variable: __penContextsMap
        /// </SecurityNote>
        [SecurityCritical]
        internal PenContexts GetPenContextsFromHwnd(PresentationSource presentationSource)
        {
            // Only safe to call from UI thread since only it will change Map.
            Debug.Assert(Dispatcher.CheckAccess());

            PenContexts penContexts = null;

            if (presentationSource != null)
            {
                __penContextsMap.TryGetValue(presentationSource, out penContexts);
            }
            return penContexts;
        }


        /// <SecurityNote>
        ///     Critical: - Calls critical methods - InputReport.InputSource,
        ///                   PenContexts.ConsiderInRange and GetPenContextsFromHwnd.
        /// </SecurityNote>
        [SecurityCritical]
        internal bool ShouldConsiderStylusInRange(RawMouseInputReport mouseInputReport)
        {
            int timestamp = mouseInputReport.Timestamp;

            // First check to see if we are close to the last time we've had a Stylus InRange
            // We consider it inrange if _lastInRangeTime+500 >= timestamp (whether we've seen
            // one within last 500ms).
            //  Here's some info on how this works...
            //      int.MaxValue - int.MinValue = -1 (subtracting any negative # from MaxValue keeps this negative)
            //      int.MinValue - int.MaxValue = 1 (subtracting any positive # from MinValue keeps this positive)
            //  So as values close to MaxValue and MinValue get farther apart the result increases from a
            //  of delta 1 and we can thus take the Abs value to use for this check.
            // Note: we don't really care if times wrap since the worst thing that would happen
            // is we'd let a mouse event slip through as you brought the stylus in range which
            // can happen today anyway.
            if (Math.Abs(unchecked(timestamp - _lastInRangeTime)) <= 500)
                return true;

            HwndSource hwndSource = mouseInputReport.InputSource as HwndSource;
            if (hwndSource != null)
            {
                PenContexts penContexts = GetPenContextsFromHwnd(hwndSource);
                if (penContexts != null)
                {
                    return penContexts.ConsiderInRange(timestamp);
                }
            }
            return false;
        }


        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: - Accesses a critical member variable: __penContextsMap
        ///                 and calls SecurityCritical method
        ///                 PenContexts.GetTabletDeviceIDPenContext.
        /// </SecurityNote>
        [SecurityCritical]
        internal PenContext GetStylusPenContextForHwnd(PresentationSource presentationSource, int tabletDeviceId)
        {
            // Only safe to call from UI thread since only it will change Map.
            Debug.Assert(Dispatcher.CheckAccess());

            if (presentationSource != null)
            {
                PenContexts penContexts;
                __penContextsMap.TryGetValue(presentationSource, out penContexts);
                if (penContexts != null)
                {
                    return penContexts.GetTabletDeviceIDPenContext(tabletDeviceId);
                }
            }
            return null;
        }

        /// <summary>
        /// A method handles WM_DEVICECHANGE message.
        /// </summary>
        /// <SecurityNote>
        ///     Critical: The method invoke critical methods -
        ///                 TabletDeviceCollection.ShouldEnableTablets, TabletDeviceCollection.UpdateTablets and EnableCore.
        ///               TreatAsSafe boundry is SystemResources.SystemThemeFilterMessage in PF.dll
        /// </SecurityNote>
        [SecurityCritical]
        private void OnDeviceChange()
        {
            Debug.Assert(!_inputEnabled, "StylusLogic has been enabled unexpectly.");

            if ( !_inputEnabled && TabletDeviceCollection.ShouldEnableTablets() )
            {
                // Create the tablet device collection!
                TabletDevices.UpdateTablets();

                // Enable stylus input on all hwnds if we have not yet done so.
                EnableCore();
            }
        }

        /// <SecurityNote>
        ///     Critical: This code calls SecurityCritical code (TabletDeviceCollection.HandleTabletAdded,
        ///             TabletDeviceCollection.UpdateTablets, PenContexts.Enable, PenContexts.Disable and
        ///             PenContexts.AddContext) and accesses SecurityCritical data __penContextsMap.
        ///             Called by StylusLogic.HandleMessage.
        ///             TreatAsSafe boundry is HwndWrapperHook class (called via HwndSource.InputFilterMessage).
        /// </SecurityNote>
        [SecurityCritical]
        private void OnTabletAdded(uint wisptisIndex)
        {
            lock ( __penContextsLock )
            {
                TabletDeviceCollection tabletDeviceCollection = TabletDevices;

                // When we receive the first WM_TABLET_ADDED message without being enabled,
                // we have to update our TabletDevices at once and enable StylusLogic
                if ( !_inputEnabled )
                {
                    tabletDeviceCollection.UpdateTablets(); // Create the tablet device collection!
                    EnableCore(); // Go and enable input now.

                    return; // We are done here.
                }

                uint tabletIndex = UInt32.MaxValue;
                // HandleTabletAdded returns true if we need to update contexts due to a change in tablet devices.
                if (tabletDeviceCollection.HandleTabletAdded(wisptisIndex, ref tabletIndex))
                {
                    if (tabletIndex != UInt32.MaxValue)
                    {
                        // Update all contexts with this new tablet device.
                        foreach (PenContexts contexts in __penContextsMap.Values)
                        {
                            contexts.AddContext(tabletIndex);
                        }
                    }
                    else
                    {
                        // rebuild all contexts and tablet collection if duplicate found.
                        foreach ( PenContexts contexts in __penContextsMap.Values )
                        {
                            contexts.Disable(false /*shutdownWorkerThread*/);
                        }

                        tabletDeviceCollection.UpdateTablets();

                        foreach ( PenContexts contexts in __penContextsMap.Values )
                        {
                            contexts.Enable();
                        }
                    }
                }
            }
        }

        /// <SecurityNote>
        ///     Critical: This code calls SecurityCritical code (PenContexts.RemoveContext and
        ///              TabletDeviceCollection.HandleTabletRemoved) and accesses SecurityCritical data
        ///              __penContextsMap.
        ///             Called by StylusLogic.HandleMessage.
        ///             TreatAsSafe boundry is HwndWrapperHook class (called via HwndSource.InputFilterMessage).
        /// </SecurityNote>
        [SecurityCritical]
        private void OnTabletRemoved(uint wisptisIndex)
        {
            // Nothing to do if the Stylus hasn't been enabled yet.
            if ( _inputEnabled )
            {
                lock ( __penContextsLock )
                {
                    if (_tabletDeviceCollection != null)
                    {
                        uint tabletIndex = _tabletDeviceCollection.HandleTabletRemoved(wisptisIndex);

                        if (tabletIndex != UInt32.MaxValue)
                        {
                            foreach (PenContexts contexts in __penContextsMap.Values)
                            {
                                contexts.RemoveContext(tabletIndex);
                            }
                        }
                    }
                }
            }
        }


        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     Critical: This code calls SecurityCritical code (PenContexts.RebuildContexts).
        ///             Called by HwndStylusInputProvider.FilterMessage.
        ///             TreatAsSafe boundry is HwndWrapperHook class (called via HwndSource.InputFilterMessage).
        /// </SecurityNote>
        [SecurityCritical]
        private void OnScreenMeasurementsChanged()
        {
            // We only need to have one of these queued up on our dispatcher.
            if (!_updatingScreenMeasurements)
            {
                _updatingScreenMeasurements = true;

                // Queue up this code to execute after the WM_DISPLAYCHANGED message
                // has been processed
                Dispatcher.BeginInvoke(DispatcherPriority.Background, _processDisplayChanged, null);
            }
        }

        /////////////////////////////////////////////////////////////////////

        /// <summary>
        /// We get this notification when the WM_ENABLE message is sent to a window.
        /// When we get this we need to disable Stylus Input from being raised similar
        /// to how we deal with OLE DragDrop.  Win32 stops all other input from going to
        /// disabled windows so we need to do the same for stylus.
        /// </summary>
        /// <SecurityNote>
        ///     Critical: This code calls SecurityCritical code (GetPenContextsFromHwnd, HwndSource.CriticalFromHwnd)
        ///                 and takes SecurityCritical data as input (hwnd).
        ///             Called by HwndStylusInputProvider.FilterMessage.
        ///             TreatAsSafe boundry is HwndWrapperHook class (called via HwndSource.InputFilterMessage).
        /// </SecurityNote>
        [SecurityCritical]
        internal void OnWindowEnableChanged(IntPtr hwnd, bool disabled)
        {
            // See if this is one of our windows.
            HwndSource sourceHit = HwndSource.CriticalFromHwnd(hwnd);

            // We need to check if the point is over the client or
            // non-client area.  We only care about being over the
            // client area.
            if (sourceHit != null)
            {
                // Find the pencontexts for this window and update it's disabled window state
                PenContexts penContexts = GetPenContextsFromHwnd(sourceHit);
                if (penContexts != null)
                {
                    penContexts.IsWindowDisabled = disabled;
                }
            }

            // See if we need to update the mouse state when going enabled.
            if (!disabled && _currentStylusDevice != null)
            {
                // If we are in air or have not fired down the set mouse in up state.
                if (_currentStylusDevice.InAir || !_currentStylusDevice.GestureWasFired)
                {
                    _mouseLeftButtonState = MouseButtonState.Released;
                    _mouseRightButtonState = MouseButtonState.Released;
                }
                else
                {
                    _mouseLeftButtonState = _currentStylusDevice.LeftIsActiveMouseButton ? MouseButtonState.Pressed : MouseButtonState.Released;
                    _mouseRightButtonState = !_currentStylusDevice.LeftIsActiveMouseButton ? MouseButtonState.Pressed : MouseButtonState.Released;
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        /// Critical - calls security critical code PenContexts.Disable,
        ///             PenContexts.Enable and TabletDevice.UpdateScreenMeasurements.
        ///
        /// TreatAsSafe - Just updates TabletDevice size info use for mapping input data.  No data goes in or out.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        internal object ProcessDisplayChanged(object oInput)
        {
            _updatingScreenMeasurements = false;

            // We don't want to rebuild the contexts and update the tabletdevice
            // measurements if the Stylus hasn't been enabled yet.
            if (_tabletDeviceCollection != null)
            {
                // NTRAID#T2-26521-2004/10/27-waynezen,
                // Invalidate the screen measurements of the tablet device.
                foreach ( TabletDevice tablet in _tabletDeviceCollection )
                {
                    tablet.UpdateScreenMeasurements();
                }
            }

            return null;
        }

        /////////////////////////////////////////////////////////////////////

        internal Matrix GetTabletToViewTransform(TabletDevice tabletDevice)
        {
            // NTRAID#Tablet_PC_Bug 26555-2004/11/3-xiaotu: Inking is offset under 120 DPI
            // Changet the TabletToViewTransform matrix to take DPI into account. The default
            // value is 96 DPI in Avalon. The device DPI value is cached after the first call
            // to this function.

            Matrix matrix = _transformToDevice;
            matrix.Invert();
            return matrix * tabletDevice.TabletToScreen;
        }

        /// <summary>
        /// Transforms a point in measure units to a point in device coordinates
        /// </summary>
        /// <param name="measurePoint">The point to transform, in measure units</param>
        /// <returns>The point in device coordinates</returns>
        internal Point DeviceUnitsFromMeasureUnits(Point measurePoint)
        {
            Point pt = measurePoint * _transformToDevice;
            pt.X = (int)Math.Round(pt.X); // Make sure we return whole numbers (pixels are whole numbers)
            pt.Y = (int)Math.Round(pt.Y);
            return pt;
        }

        /// <summary>
        /// Transforms a point in measure units to a point in device coordinates
        /// </summary>
        /// <param name="measurePoint">The point to transform, in measure units</param>
        /// <returns>The point in device coordinates</returns>
        internal Point MeasureUnitsFromDeviceUnits(Point measurePoint)
        {
            Matrix matrix = _transformToDevice;
            matrix.Invert();
            return measurePoint * matrix;
        }


        // This is used to determine whether we postpone promoting mouse move events
        // from stylus events.
        internal void SetLastRawMouseActions(RawMouseActions actions)
        {
            _lastRawMouseAction = actions;
        }

#if !MULTICAPTURE
        private DeferredElementTreeState StylusOverTreeState
        {
            get
            {
                if (_stylusOverTreeState == null)
                {
                    _stylusOverTreeState = new DeferredElementTreeState();
                }

                return _stylusOverTreeState;
            }
        }

        private DeferredElementTreeState StylusCaptureWithinTreeState
        {
            get
            {
                if (_stylusCaptureWithinTreeState == null)
                {
                    _stylusCaptureWithinTreeState = new DeferredElementTreeState();
                }

                return _stylusCaptureWithinTreeState;
            }
        }
#endif

        internal class StagingAreaInputItemList : List<StagingAreaInputItem>
        {
            internal void AddItem(StagingAreaInputItem item)
            {
                Add(item);
                IncrementVersion();
            }

            internal long Version { get { return _version; } }

            internal long IncrementVersion()
            {
                return unchecked( ++_version );
            }
            long _version;
        }

        /////////////////////////////////////////////////////////////////////

        private Matrix _transformToDevice = Matrix.Identity;
        private bool _transformInitialized;

        /////////////////////////////////////////////////////////////////////

        /// <SecurityNote>
        ///     This data is not safe to expose as it holds refrence to PresentationSource
        /// </SecurityNote>
        private SecurityCriticalData<InputManager> _inputManager;

        DispatcherOperationCallback  _dlgInputManagerProcessInput;

        object _stylusEventQueueLock = new object();

        /// <SecurityNote>
        /// Critical - Marked critical to prevent inadvertant code from
        ///             modifying raw stylus input reports.
        /// </SecurityNote>
        [SecurityCritical]
        Queue<RawStylusInputReport> _queueStylusEvents = new Queue<RawStylusInputReport>();

        int             _lastStylusDeviceId;
        bool            _lastMouseMoveFromStylus = true; // Default to true to help first time use issues.

        // Information used to distinguish double-taps (actually, multi taps) from
        // multiple independent taps.
        private int _doubleTapDeltaTime = 800;  // this is in milli-seconds
        private int _doubleTapDelta = 15;  // The default double tap distance is .1 mm values (default is 1.5mm)
        private int _cancelDelta = 10;        // The move distance is 1.0mm default (value in .1 mm)

        private RawMouseActions _lastRawMouseAction = RawMouseActions.None;
        private bool _waitingForDelegate = false;

        private MouseButtonState _mouseLeftButtonState = MouseButtonState.Released;
        private MouseButtonState _mouseRightButtonState = MouseButtonState.Released;

        private StylusPlugInCollection _activeMousePlugInCollection;
        private StylusPointDescription _mousePointDescription;

        // From old instanced Stylus class
        private EventHandler    _shutdownHandler;

        bool _tabletDeviceCollectionDisposed;
        TabletDeviceCollection  _tabletDeviceCollection;
        StylusDevice            _currentStylusDevice;

        int                    _lastInRangeTime;
        bool                   _triedDeferringMouseMove;
        /// <SecurityNote>
        /// Critical - Marked critical to prevent inadvertant code from
        ///             modifying deferred mouse move information.
        /// </SecurityNote>
        [SecurityCritical]
        RawMouseInputReport              _deferredMouseMove;

        DispatcherOperationCallback  _processDeferredMouseMove;

        /// <SecurityNote>
        /// Critical - Marked critical to prevent inadvertant code from
        ///             modifying this deferred mouse deactivate information.
        /// </SecurityNote>
        [SecurityCritical]
        RawMouseInputReport              _mouseDeactivateInputReport;

        bool                             _inputEnabled = false;
        bool                             _updatingScreenMeasurements = false;
        DispatcherOperationCallback  _processDisplayChanged;
        object                           __penContextsLock = new object();

        /// <SecurityNote>
        /// Critical - Marked critical to prevent inadvertant spread
        ///             to transparent code.
        /// </SecurityNote>
        [SecurityCritical]
        Dictionary<object, PenContexts>   __penContextsMap = new Dictionary<object, PenContexts>(2);

        object                            __stylusDeviceLock = new object();
        Dictionary<int, StylusDevice>     __stylusDeviceMap = new Dictionary<int, StylusDevice>(2);

        bool                              _inDragDrop;
        bool                              _processingQueuedEvent;

        bool                              _stylusDeviceInRange;

        bool                     _seenRealMouseActivate;

#if !MULTICAPTURE
        IInputElement _stylusCapture;
        IInputElement _stylusOver;
        DeferredElementTreeState _stylusOverTreeState;
        DeferredElementTreeState _stylusCaptureWithinTreeState;

        private DependencyPropertyChangedEventHandler _overIsEnabledChangedEventHandler;
        private DependencyPropertyChangedEventHandler _overIsVisibleChangedEventHandler;
        private DependencyPropertyChangedEventHandler _overIsHitTestVisibleChangedEventHandler;
        private DispatcherOperationCallback _reevaluateStylusOverDelegate;
        private DispatcherOperation _reevaluateStylusOverOperation;

        private DependencyPropertyChangedEventHandler _captureIsEnabledChangedEventHandler;
        private DependencyPropertyChangedEventHandler _captureIsVisibleChangedEventHandler;
        private DependencyPropertyChangedEventHandler _captureIsHitTestVisibleChangedEventHandler;
        private DispatcherOperationCallback _reevaluateCaptureDelegate;
        private DispatcherOperation _reevaluateCaptureOperation;
#endif
    }
}
