namespace ApiTester.Ui.Components;

public sealed record ErrorPanelModel(string Message, string? Details);

public sealed record EmptyStateModel(string Message);
