


using System.Text;

namespace Game.UI
{
    
    public interface ICursorTooltipSource
    {
        
        void TryBuildTooltip(StringBuilder sb);
    }
}
