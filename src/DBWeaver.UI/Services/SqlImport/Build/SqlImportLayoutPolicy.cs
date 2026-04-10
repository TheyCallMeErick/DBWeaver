using Avalonia;

namespace DBWeaver.UI.Services.SqlImport.Build;

public static class SqlImportLayoutPolicy
{
    public static ImportLayout Default { get; } = new(BaseX: 80, BaseY: 120, ColGap: 280, RowGap: 220);
}

public readonly record struct SqlImportLayoutCalculator(ImportLayout Layout)
{
    public double ResultY(int sourceCount) => Layout.BaseY + (sourceCount - 1) * Layout.RowGap / 2.0;

    public Point TablePosition(int sourceIndex) => new(Layout.BaseX, Layout.BaseY + sourceIndex * Layout.RowGap);

    public Point JoinPosition(int sourceIndex) => new(Layout.BaseX + Layout.ColGap, Layout.BaseY + sourceIndex * Layout.RowGap - 80);

    public Point ResultPosition(double resultY) => new(Layout.BaseX + Layout.ColGap * 3, resultY);

    public Point ColumnSetPosition(double resultY) => new(Layout.BaseX + Layout.ColGap * 2, resultY);

    public Point SubqueryPosition(int sourceCount) => new(Layout.BaseX + Layout.ColGap * 2, Layout.BaseY + sourceCount * Layout.RowGap);

    public Point ComparisonPosition(int sourceCount) => new(Layout.BaseX + Layout.ColGap, Layout.BaseY + sourceCount * Layout.RowGap);

    public Point WherePosition(int sourceCount) => new(Layout.BaseX + Layout.ColGap * 2, Layout.BaseY + sourceCount * Layout.RowGap);

    public Point HavingCountPosition(int sourceCount) => new(Layout.BaseX + Layout.ColGap, Layout.BaseY + sourceCount * Layout.RowGap + 80);

    public Point HavingComparisonPosition(int sourceCount) => new(Layout.BaseX + Layout.ColGap * 2, Layout.BaseY + sourceCount * Layout.RowGap + 80);

    public Point TopPosition(double resultY) => new(Layout.BaseX + Layout.ColGap * 3, resultY - 120);
}
