
using Core.UI;

public interface IUIScreen
{
    ScreenType ScreenType { get; }

    void Show();
    void Hide();
}