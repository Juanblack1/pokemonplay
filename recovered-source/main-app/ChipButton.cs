using System.Windows.Forms;



internal sealed class ChipButton : ThemeButton

{

	public ChipButton(string text)

		: base(text, ButtonKind.Ghost)

	{

		Height = 30;

		Width = 116;

		Font = AppTheme.CaptionBold;

		Margin = new Padding(0, 0, 8, 0);

	}

}
