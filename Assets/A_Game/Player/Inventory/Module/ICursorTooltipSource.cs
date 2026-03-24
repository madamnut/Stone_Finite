using System.Text;

namespace Game.Player
{
    
    public interface ICursorTooltipSource
    {
        void TryBuildTooltip(StringBuilder sb);
    }
}
