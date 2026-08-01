using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempFileCleaner
{
    /// <summary>
    /// The IsBusy attached property for anything that wants to flag if a control is busy.
    /// </summary>
    public class IsBusyProperty : BaseAttachedProperty<IsBusyProperty, bool>
    {
    }
}
