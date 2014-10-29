//------------------------------------------------------------------------------
// <copyright file="ComboBoxRenderer.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------
namespace System.Windows.Forms {

    using System;
    using System.Drawing;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows.Forms.Internal;
    using System.Windows.Forms.VisualStyles;
    using Microsoft.Win32;


    /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer"]/*' />
    /// <devdoc>
    ///    <para>
    ///       This is a rendering class for the ComboBox control.
    ///    </para>
    /// </devdoc>
    public sealed class ComboBoxRenderer {

        //Make this per-thread, so that different threads can safely use these methods.
        [ThreadStatic]
        private static VisualStyleRenderer visualStyleRenderer = null;
        private static readonly VisualStyleElement ComboBoxElement = VisualStyleElement.ComboBox.DropDownButton.Normal;
        private static readonly VisualStyleElement TextBoxElement = VisualStyleElement.TextBox.TextEdit.Normal;

        //cannot instantiate
        private ComboBoxRenderer() {
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.IsSupported"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Returns true if this class is supported for the current OS and user/application settings, 
        ///       otherwise returns false.
        ///    </para>
        /// </devdoc>
        public static bool IsSupported {
            get {
                return VisualStyleRenderer.IsSupported; // no downlevel support
            }
        }

        private static void DrawBackground(Graphics g, Rectangle bounds, ComboBoxState state) {
            visualStyleRenderer.DrawBackground(g, bounds);
            //for disabled comboboxes, comctl does not use the window backcolor, so
            // we don't refill here in that case.
            if (state != ComboBoxState.Disabled) {
                Color windowColor = visualStyleRenderer.GetColor(ColorProperty.FillColor);
                if (windowColor != SystemColors.Window) {
                    Rectangle fillRect = visualStyleRenderer.GetBackgroundContentRectangle(g, bounds);
                    fillRect.Inflate(-2, -2);
                    //then we need to re-fill the background.
                    g.FillRectangle(SystemBrushes.Window, fillRect);
                }
            }
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawTextBox"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders the textbox part of a ComboBox control.
        ///    </para>
        /// </devdoc>
        [
            SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters") // Using Graphics instead of IDeviceContext intentionally
        ]
        public static void DrawTextBox(Graphics g, Rectangle bounds, ComboBoxState state) {
            if (visualStyleRenderer == null) {
                visualStyleRenderer = new VisualStyleRenderer(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }
            else {
                visualStyleRenderer.SetParameters(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }

            DrawBackground(g, bounds, state);
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawTextBox1"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders the textbox part of a ComboBox control.
        ///    </para>
        /// </devdoc>
        public static void DrawTextBox(Graphics g, Rectangle bounds, string comboBoxText, Font font, ComboBoxState state) {
            DrawTextBox(g, bounds, comboBoxText, font, TextFormatFlags.TextBoxControl, state);
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawTextBox2"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders the textbox part of a ComboBox control.
        ///    </para>
        /// </devdoc>
        public static void DrawTextBox(Graphics g, Rectangle bounds, string comboBoxText, Font font, Rectangle textBounds, ComboBoxState state) {
            DrawTextBox(g, bounds, comboBoxText, font, textBounds, TextFormatFlags.TextBoxControl, state);
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawTextBox3"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders the textbox part of a ComboBox control.
        ///    </para>
        /// </devdoc>
        public static void DrawTextBox(Graphics g, Rectangle bounds, string comboBoxText, Font font, TextFormatFlags flags, ComboBoxState state) {
            if (visualStyleRenderer == null) {
                visualStyleRenderer = new VisualStyleRenderer(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }
            else {
                visualStyleRenderer.SetParameters(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }

            Rectangle textBounds = visualStyleRenderer.GetBackgroundContentRectangle(g, bounds);
            textBounds.Inflate(-2,-2);
            DrawTextBox(g, bounds, comboBoxText, font, textBounds, flags, state);
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawTextBox4"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders the textbox part of a ComboBox control.
        ///    </para>
        /// </devdoc>
        [
            SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters") // Using Graphics instead of IDeviceContext intentionally
        ]
        public static void DrawTextBox(Graphics g, Rectangle bounds, string comboBoxText, Font font, Rectangle textBounds, TextFormatFlags flags, ComboBoxState state) {
            if (visualStyleRenderer == null) {
                visualStyleRenderer = new VisualStyleRenderer(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }
            else {
                visualStyleRenderer.SetParameters(TextBoxElement.ClassName, TextBoxElement.Part, (int)state);
            }

            DrawBackground(g, bounds, state);
            Color textColor = visualStyleRenderer.GetColor(ColorProperty.TextColor);
            TextRenderer.DrawText(g, comboBoxText, font, textBounds, textColor, flags);
        }

        /// <include file='doc\ComboBoxRenderer.uex' path='docs/doc[@for="ComboBoxRenderer.DrawDropDownButton"]/*' />
        /// <devdoc>
        ///    <para>
        ///       Renders a ComboBox drop-down button.
        ///    </para>
        /// </devdoc>
        [
            SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters") // Using Graphics instead of IDeviceContext intentionally
        ]
        public static void DrawDropDownButton(Graphics g, Rectangle bounds, ComboBoxState state) {
            if (visualStyleRenderer == null) {
                visualStyleRenderer = new VisualStyleRenderer(ComboBoxElement.ClassName, ComboBoxElement.Part, (int)state);
            }
            else {
                visualStyleRenderer.SetParameters(ComboBoxElement.ClassName, ComboBoxElement.Part, (int)state);
            }

            visualStyleRenderer.DrawBackground(g, bounds);
        }
    }
}
