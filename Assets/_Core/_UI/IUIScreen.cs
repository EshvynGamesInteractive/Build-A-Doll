
using _Core._UI;
using _Core.UI;
using Core.UI;

public interface IUIScreen
{
    ScreenType ScreenType { get; }

    void Show();
    void Hide();
}