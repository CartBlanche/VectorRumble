using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// A box over the screen with a question (two buttons) or a notice (one). The button that backs out is on the left and
    /// the one that goes ahead on the right; the question says which of them is selected when the box opens.
    /// </summary>
    class MessageBoxScreen : FrameMenuScreen
    {
        public event EventHandler<EventArgs> Accepted;
        public event EventHandler<EventArgs> Cancelled;

        /// <summary>A question: <paramref name="cancelLabel"/> (left) leaves things as they are, <paramref name="acceptLabel"/> (right) goes ahead, and is selected first when <paramref name="acceptIsDefault"/>.</summary>
        public MessageBoxScreen(string heading, string body, string acceptLabel, string cancelLabel, bool acceptIsDefault = false)
        {
            Layout = FrameLayout.Modal;
            Heading = heading;
            ModalMessage = body ?? "";
            ShowBack = false;
            IsPopup = true;

            if (cancelLabel != null)
            {
                var cancel = new FrameRow(cancelLabel);
                cancel.Selected += Cancel;
                Rows.Add(cancel);
            }

            var accept = new FrameRow(acceptLabel);
            accept.Selected += Accept;
            Rows.Add(accept);

            if (acceptIsDefault)
            {
                SelectedIndex = Rows.Count - 1;
            }
        }

        /// <summary>A notice, with one button.</summary>
        public MessageBoxScreen(string message) : this(message, "", Strings.Value_OK, null)
        {
        }

        void Accept()
        {
            Accepted?.Invoke(this, EventArgs.Empty);
            ExitScreen();
        }

        void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            ExitScreen();
        }

        // escape is "no" for a question, and "ok" for a notice
        protected override void OnCancel()
        {
            if (Rows.Count > 1)
            {
                Cancel();
            }
            else
            {
                Accept();
            }
        }

        // a modal has no title above it
        protected override void DrawTitle(bool large)
        {
        }
    }
}
