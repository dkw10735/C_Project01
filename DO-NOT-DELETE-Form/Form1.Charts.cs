using System;
using System.Drawing;
using System.Windows.Forms;
using MarketCalc.Core;

namespace WindowsFormsApp1
{
    public partial class Form1
    {
        // 그래프 대신 '이전→이후 가격 변동표'를 차트가 있던 영역에 둔다.
        private TableLayoutPanel _priceTable;

        private const int PtColName = 0;
        private const int PtColPrev = 1;
        private const int PtColAfter = 2;
        private const int PtColChange = 3;
        private const int PtColCount = 4;

        /// <summary>이전 가격 표를 (차트가 있던) 좌하단 영역에 만든다.</summary>
        private void BuildPriceTable()
        {
            _priceTable = new TableLayoutPanel
            {
                Location = new Point(9, 248),
                Size = new Size(360, 210),
                AutoScroll = true,
                ColumnCount = PtColCount,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_priceTable);

            label8.Text = "이전 → 이후 가격 변동표"; // 캡션(기존 '시세 추이')
        }

        /// <summary>계산 결과로 이전/이후 가격·변동률 표를 채운다.</summary>
        private void UpdatePriceTable(CalculationResult result, Unit unit)
        {
            var t = _priceTable;
            t.SuspendLayout();
            t.Controls.Clear();
            t.RowStyles.Clear();
            t.ColumnStyles.Clear();

            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f)); // 물품명
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f)); // 이전 가격
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f)); // 이후 가격
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f)); // 변동률

            t.RowCount = result.Items.Count + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 헤더
            for (int i = 0; i < result.Items.Count; i++)
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            t.Controls.Add(PtHeader("물품명"), PtColName, 0);
            t.Controls.Add(PtHeader("이전 가격"), PtColPrev, 0);
            t.Controls.Add(PtHeader("이후 가격"), PtColAfter, 0);
            t.Controls.Add(PtHeader("변동률"), PtColChange, 0);

            for (int i = 0; i < result.Items.Count; i++)
            {
                var r = result.Items[i];
                int row = i + 1;
                t.Controls.Add(PtCell(r.ItemName), PtColName, row);
                t.Controls.Add(PtCell(unit.Format(r.PreviousSalePrice)), PtColPrev, row);
                t.Controls.Add(PtCell(unit.Format(r.SalePrice)), PtColAfter, row);
                t.Controls.Add(PtChangeCell(r.ChangeRate), PtColChange, row);
            }

            t.ResumeLayout();
        }

        private static Label PtHeader(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("굴림", 9f, FontStyle.Bold),
            Margin = new Padding(3, 4, 3, 2)
        };

        private static Label PtCell(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 4, 3, 2)
        };

        /// <summary>변동률 셀: 상승은 빨강(▲), 하락은 파랑(▼), 보합은 회색.</summary>
        private static Label PtChangeCell(decimal changeRate)
        {
            double pct = (double)changeRate * 100.0;
            string arrow = pct > 0.0 ? "▲" : pct < 0.0 ? "▼" : "－";
            Color color = pct > 0.0 ? Color.Firebrick : pct < 0.0 ? Color.RoyalBlue : Color.Gray;
            return new Label
            {
                Text = $"{arrow}{Math.Abs(pct):0.0}%",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = color,
                Margin = new Padding(3, 4, 3, 2)
            };
        }
    }
}
