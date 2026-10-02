using System;
internal sealed class HeroBanner : PixelHeader
{
	private int gameCount;
	internal int GameCount => gameCount;

	public HeroBanner(int gameCount)
		: base("Biblioteca de jogos", BuildSubtitle(gameCount), 80)
	{
		this.gameCount = gameCount;
	}

	internal void UpdateGameCount(int count, DateTimeOffset refreshedAt)
	{
		gameCount = count;
		SetSubtitle($"Atualizado às {refreshedAt.ToLocalTime():HH:mm} · {count} jogos prontos para jogar");
	}

	private static string BuildSubtitle(int count) => count + " jogos prontos para jogar · selecione uma aventura";
}
