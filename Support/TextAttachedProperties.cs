using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TempFileCleaner
{
    /// <summary>
    /// Focus property for any control that wants keyboard focus.
    /// </summary>
    public class IsFocusedProperty : BaseAttachedProperty<IsFocusedProperty, bool>
    {
        public override void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is Control control))
                return;

            // Focus to the control once it is loaded.
            control.Loaded += (s, se) => control.Focus();
        }
    }

    /// <summary>
    /// Focuses (keyboard focus) this element if true
    /// </summary>
    public class FocusProperty : BaseAttachedProperty<FocusProperty, bool>
    {
        public override void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            // If we don't have a control, return
            if (!(sender is Control control))
                return;

            // Focus this control
            if ((bool)e.NewValue)
                control.Focus();
        }
    }


    /// <summary>
    /// Focuses (keyboard focus) and selects all text in this element if true
    /// </summary>
    public class FocusAndSelectProperty : BaseAttachedProperty<FocusAndSelectProperty, bool>
    {
        public override void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            // If we don't have a control, return
            if (sender is TextBoxBase control)
            {
                if ((bool)e.NewValue)
                {
                    // Focus this control
                    control.Focus();

                    // Select all text
                    control.SelectAll();
                }
            }
            else if (sender is PasswordBox password)
            {
                if ((bool)e.NewValue)
                {
                    // Focus this control
                    password.Focus();

                    // Select all text
                    password.SelectAll();
                }
            }
        }
    }

}
