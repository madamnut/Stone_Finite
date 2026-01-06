using System.Text;

public interface ICursorTooltipSource
{
    void TryBuildTooltip(StringBuilder sb);
}
