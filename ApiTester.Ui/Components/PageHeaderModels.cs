namespace ApiTester.Ui.Components;

public sealed record BreadcrumbItem(string Text, string? Url = null);

public sealed record PageHeaderModel(string Title, string? Subtitle, IReadOnlyList<BreadcrumbItem> Breadcrumbs);
