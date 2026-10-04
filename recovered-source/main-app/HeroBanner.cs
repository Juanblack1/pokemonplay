using System;
internal sealed class HeroBanner : PixelHeader
{
	private int gameCount;
	private int visibleGameCount;
	private bool hasActiveFilters;
	private DateTimeOffset? refreshedAt;
	internal int GameCount => gameCount;

	public HeroBanner(int gameCount)
		: base("Biblioteca de jogos", BuildSubtitle(gameCount), 80)
	{
		this.gameCount = gameCount;
		visibleGameCount = gameCount;
	}

	internal void UpdateGameCount(int count, DateTimeOffset refreshedAt)
	{
		gameCount = count;
		this.refreshedAt = refreshedAt;
		UpdateSubtitle();
	}

	internal void UpdateVisibleGameCount(int count, bool filtered)
	{
		visibleGameCount = count;
		hasActiveFilters = filtered;
		UpdateSubtitle();
	}

	private void UpdateSubtitle()
	{
		string subtitle = hasActiveFilters
			? $"Exibindo {visibleGameCount} de {gameCount} jogos encontrados"
			: refreshedAt.HasValue
				? $"Atualizado às {refreshedAt.Value.ToLocalTime():HH:mm} · {gameCount} jogos prontos para jogar"
				: BuildSubtitle(gameCount);
		SetSubtitle(subtitle);
	}

	private static string BuildSubtitle(int count) => count + " jogos prontos para jogar · selecione uma aventura";
}
