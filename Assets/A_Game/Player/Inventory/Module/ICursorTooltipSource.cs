using System.Text;
using Game.Player;

namespace Game.UI
{
    
    public interface ICursorTooltipSource
    {
        void TryBuildTooltip(StringBuilder sb);
    }
}
