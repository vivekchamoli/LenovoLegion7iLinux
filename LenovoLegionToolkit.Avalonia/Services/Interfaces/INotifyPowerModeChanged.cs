using System;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface INotifyPowerModeChanged
    {
        event EventHandler<PowerMode>? PowerModeChanged;
    }
}