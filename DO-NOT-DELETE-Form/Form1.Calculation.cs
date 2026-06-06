using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MarketCalc.Core;

namespace WindowsFormsApp1
{
    public partial class Form1
    {
        // 세로 리스트 테이블: 행 = 물품(하나), 열 = 필드.
        // 0 물품명 | 1 생산비 | 2 이전가 | 3 마진% (입력) | 4 시세 | 5 판매가 | 6 희망가 (출력) | 7 삭제
        private const int ColName = 0;
        private const int ColCost = 1;
        private const int ColPrev = 2;
        private const int ColMargin = 3;
        private const int ColMarketPrice = 4;
        private const int ColSalePrice = 5;
        private const int ColRetailPrice = 6;
        private const int ColRemove = 7;
        private const int TableColCount = 8;
        private const int MaxItems = 20; // 스크롤로 수용

        private readonly MarketCalculator _calculator = new MarketCalculator();

        /// <summary>물품 한 행의 입력칸·출력라벨·가져온 이력을 묶는다.</summary>
        private sealed class ItemRow
        {
            public TextBox Name;
            public TextBox Cost;
            public TextBox Prev;   // 이전 가격(비우면 자동=시작일)
            public TextBox Margin;
            public Label MarketPrice;
            public Label SalePrice;
            public Label RetailPrice;
            public Button Remove;
            public MarketItem Imported; // 가져온 날짜별 이력(없으면 null → 단일 시점)
        }

        private readonly System.Collections.Generic.List<ItemRow> _rows =
            new System.Collections.Generic.List<ItemRow>();

        /// <summary>'＋ 물품 추가' 행 라벨(항상 마지막).</summary>
        private Label _addRowLabel;

        /// <summary>희망가 열 헤더(소매마진율을 제목에 표시).</summary>
        private Label _retailHeader;

        /// <summary>마지막 계산 결과 (차트/내보내기 단계에서 재사용).</summary>
        private CalculationResult _lastResult;

        /// <summary>소매 마진율 (희망소비자가격 = 판매가 / (1−마진)). 세부 설정으로 조정.</summary>
        private decimal _retailMargin = 0.30m;

        /// <summary>마지막 계산에 사용한 단위 (내보내기용).</summary>
        private Unit _lastUnit;

        /// <summary>기준소득 (소득계수 분모). 평균소득 세부설정에서 지정.</summary>
        private decimal _referenceIncome;

        /// <summary>소비성향 (MPC). 재산 추이 시뮬레이션에 사용. 세부설정으로 조정.</summary>
        private decimal _consumptionPropensity = 0.7m;

        /// <summary>변동 시뮬레이션(사인+노이즈) on/off. label4 클릭으로 토글. 기본 꺼짐.</summary>
        private bool _useVolatility = false;

        /// <summary>
        /// 테이블을 세로 리스트로 구성한다. 기본 물품 3개로 시작하고,
        /// '＋ 물품 추가'로 행을 늘리며(최대 MaxItems) 넘치면 세로 스크롤된다.
        /// </summary>
        private void BuildEditableTable()
        {
            tableLayoutPanel1.AutoScroll = true;

            _addRowLabel = new Label
            {
                Text = "＋ 물품 추가",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = System.Drawing.Color.SteelBlue,
                Cursor = Cursors.Hand,
                Margin = new Padding(3, 5, 3, 3)
            };
            _addRowLabel.Click += (s, e) => AddItemRow();
            new ToolTip().SetToolTip(_addRowLabel, "물품 행을 추가합니다.");

            _rows.Clear();
            for (int i = 0; i < 3; i++)
                _rows.Add(CreateItemRow($"물품{i + 1}"));

            RebuildTable();
        }

        /// <summary>새 물품 행(컨트롤 묶음)을 생성한다(테이블 배치는 RebuildTable에서).</summary>
        private ItemRow CreateItemRow(string seedName)
        {
            var row = new ItemRow
            {
                Name = new TextBox { Dock = DockStyle.Fill, Text = seedName },
                Cost = new TextBox { Dock = DockStyle.Fill, Text = "0" },
                Prev = new TextBox { Dock = DockStyle.Fill, Text = "" }, // 비우면 자동(시작일)
                Margin = new TextBox { Dock = DockStyle.Fill, Text = "20%" },
                MarketPrice = OutLabel(),
                SalePrice = OutLabel(),
                RetailPrice = OutLabel()
            };
            row.Remove = new Button
            {
                Text = "✕",
                Dock = DockStyle.Fill,
                Margin = new Padding(1),
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            row.Remove.Click += (s, e) => RemoveItemRow(row);
            new ToolTip().SetToolTip(row.Remove, "이 물품 행 삭제");
            row.Prev.PlaceholderText = "자동";
            new ToolTip().SetToolTip(row.Prev, "이전 가격(판매가). 비우면 조회 시작일 기준 자동 계산.");
            return row;
        }

        private static Label OutLabel() =>
            new Label { Text = "0", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 0) };

        /// <summary>물품 행을 하나 추가한다(최대 MaxItems).</summary>
        private void AddItemRow()
        {
            if (_rows.Count >= MaxItems)
            {
                MessageBox.Show($"물품은 최대 {MaxItems}개까지 추가할 수 있습니다.", "물품 추가",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _rows.Add(CreateItemRow($"물품{_rows.Count + 1}"));
            RebuildTable();
        }

        /// <summary>물품 행을 삭제한다(최소 1개는 유지).</summary>
        private void RemoveItemRow(ItemRow row)
        {
            if (_rows.Count <= 1)
            {
                MessageBox.Show("최소 1개의 물품 행은 필요합니다.", "물품 삭제",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _rows.Remove(row);
            RebuildTable();
        }

        /// <summary>헤더 + 모든 물품 행 + 추가 행을 테이블에 다시 배치한다.</summary>
        private void RebuildTable()
        {
            var t = tableLayoutPanel1;
            t.SuspendLayout();
            t.Controls.Clear();
            t.RowStyles.Clear();
            t.ColumnStyles.Clear();

            t.ColumnCount = TableColCount;
            // 입력 4열(이전가 포함) + 출력 3열은 비율, 삭제 버튼은 고정폭
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19f)); // 물품명
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f)); // 생산비
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f)); // 이전가
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f)); // 마진%
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f)); // 시세
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f)); // 판매가
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f)); // 희망가
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f)); // 삭제

            t.RowCount = _rows.Count + 2; // 헤더 + 물품들 + 추가행
            t.RowStyles.Clear();
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 헤더
            for (int i = 0; i < _rows.Count; i++)
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 추가행

            // --- 헤더(0행) ---
            t.Controls.Add(HeaderLabel("물품명"), ColName, 0);
            t.Controls.Add(HeaderLabel("생산비"), ColCost, 0);
            t.Controls.Add(HeaderLabel("이전가"), ColPrev, 0);
            t.Controls.Add(HeaderLabel("마진%"), ColMargin, 0);
            t.Controls.Add(HeaderLabel("시세"), ColMarketPrice, 0);
            t.Controls.Add(HeaderLabel("판매가"), ColSalePrice, 0);

            _retailHeader = HeaderLabel(RetailHeaderText());
            _retailHeader.Cursor = Cursors.Hand;
            _retailHeader.Click += (s, e) => EditRetailMargin();
            new ToolTip().SetToolTip(_retailHeader, "클릭: 희망가 = 판매가 ÷ (1−소매마진율)의 마진율 조정");
            t.Controls.Add(_retailHeader, ColRetailPrice, 0);

            // --- 물품 행들 ---
            for (int i = 0; i < _rows.Count; i++)
            {
                int r = i + 1;
                var row = _rows[i];
                t.Controls.Add(row.Name, ColName, r);
                t.Controls.Add(row.Cost, ColCost, r);
                t.Controls.Add(row.Prev, ColPrev, r);
                t.Controls.Add(row.Margin, ColMargin, r);
                t.Controls.Add(row.MarketPrice, ColMarketPrice, r);
                t.Controls.Add(row.SalePrice, ColSalePrice, r);
                t.Controls.Add(row.RetailPrice, ColRetailPrice, r);
                t.Controls.Add(row.Remove, ColRemove, r);
            }

            // --- 추가행(전체 열 span) ---
            t.Controls.Add(_addRowLabel, 0, _rows.Count + 1);
            t.SetColumnSpan(_addRowLabel, TableColCount);

            t.ResumeLayout();
        }

        private static Label HeaderLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new System.Drawing.Font("굴림", 9f, System.Drawing.FontStyle.Bold),
            Margin = new Padding(3, 6, 3, 2)
        };

        private string RetailHeaderText() =>
            $"희망가(마진{Percent.Format(_retailMargin)}) ⚙";

        /// <summary>소매 마진율(%) 입력 대화상자.</summary>
        private void EditRetailMargin()
        {
            string cur = Percent.Format(_retailMargin);
            string input = TextPrompt.Show(this, "소매 마진율", "희망소비자가격 마진율(%) (예: 30)", cur);
            if (input == null) return;
            if (!Percent.TryParse(input, out decimal m) || m >= 1m)
            {
                MessageBox.Show("0 이상 100 미만의 퍼센트를 입력하세요.", "입력 오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _retailMargin = m;
            if (_retailHeader != null) _retailHeader.Text = RetailHeaderText(); // 헤더에 마진율 반영
        }

        /// <summary>평균소득 라벨을 클릭하면 고/중/저 소득 세부 설정 창을 연다.</summary>
        private void OpenIncomeSettings()
        {
            var result = IncomeDialog.Show(this);
            if (result.HasValue)
            {
                textBox5.Text = result.Value.average.ToString("0.##", CultureInfo.InvariantCulture);
                _referenceIncome = result.Value.reference;
            }
        }

        /// <summary>"확인" 버튼: 입력을 모아 계산하고 테이블·안정도를 갱신한다.</summary>
        private void RunCalculation()
        {
            CalculationParameters p;
            List<ItemInput> items;
            try
            {
                p = BuildParameters();
                items = ReadItems(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CalculationResult result;
            try
            {
                result = _calculator.Calculate(items, p);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "계산 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lastResult = result;
            _lastUnit = p.Unit;
            WriteResults(result, p.Unit);
            UpdateStability(result);
            UpdatePriceTable(result, p.Unit);
        }

        /// <summary>GUI 입력값을 CalculationParameters로 수집·검증한다(공통 비율은 기본값).</summary>
        private CalculationParameters BuildParameters()
        {
            if (!Percent.TryParse(comboBox1.Text, out decimal taxRate))
                throw new InvalidOperationException("세율을 퍼센트로 입력하세요 (예: 10%).");

            if (!(comboBox2.SelectedItem is Unit unit))
                throw new InvalidOperationException("단위를 선택하세요.");

            // listBox 기본 마진율 = 물품별 마진율이 비어있을 때의 공통 기본값
            decimal defaultMargin = 0m;
            if (listBox1.SelectedItem is string ratioText)
                Percent.TryParse(ratioText, out defaultMargin);

            var (start, end) = NormalizedDateRange();

            return new CalculationParameters
            {
                UseIncomeFactor = checkBox1.Checked,     // 소득 기준
                UseInflationFactor = checkBox2.Checked,  // 물가 기준
                UseMarginFactor = checkBox3.Checked,     // 판매 기준 (마진)
                AverageIncome = ParseIncome(),
                ReferenceIncome = _referenceIncome,
                TaxRate = taxRate,
                Unit = unit,
                CalcRatio = defaultMargin,
                RetailMargin = _retailMargin,
                Quantity = (int)numericUpDown1.Value,
                StartDate = start,
                EndDate = end,
                ConsumptionPropensity = _consumptionPropensity,
                UseVolatility = _useVolatility
            };
        }

        private decimal ParseIncome()
        {
            if (string.IsNullOrWhiteSpace(textBox5.Text)) return 0m;
            if (!decimal.TryParse(textBox5.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal income)
                && !decimal.TryParse(textBox5.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out income))
                throw new InvalidOperationException("평균 소득은 숫자여야 합니다.");
            if (income < 0m) throw new InvalidOperationException("평균 소득은 0 이상이어야 합니다.");
            return income;
        }

        private (DateTime start, DateTime end) NormalizedDateRange()
        {
            DateTime start = dateTimePicker2.Value.Date;
            DateTime end = dateTimePicker1.Value.Date;
            return start <= end ? (start, end) : (end, start);
        }

        /// <summary>각 물품 행에서 이름·생산비·마진율을 읽어 ItemInput 목록을 만든다.</summary>
        private List<ItemInput> ReadItems(CalculationParameters p)
        {
            var items = new List<ItemInput>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                string name = string.IsNullOrWhiteSpace(row.Name.Text) ? $"물품{i + 1}" : row.Name.Text.Trim();
                decimal cost = ParseDecimal(row.Cost.Text);

                decimal? margin = null;
                if (!string.IsNullOrWhiteSpace(row.Margin.Text))
                {
                    if (!Percent.TryParse(row.Margin.Text, out decimal m))
                        throw new InvalidOperationException($"{name}의 마진율을 퍼센트로 입력하세요 (예: 20%).");
                    margin = m;
                }

                // 이전 가격: 입력하면 그 값, 비우면 null(시작일 기준 자동)
                decimal? prevPrice = null;
                if (!string.IsNullOrWhiteSpace(row.Prev.Text))
                {
                    if (!decimal.TryParse(row.Prev.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal pv)
                        && !decimal.TryParse(row.Prev.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out pv))
                        throw new InvalidOperationException($"{name}의 이전 가격은 숫자여야 합니다 (비우면 자동).");
                    if (pv < 0m) throw new InvalidOperationException($"{name}의 이전 가격은 0 이상이어야 합니다.");
                    prevPrice = pv;
                }

                // 가져온 날짜별 이력이 있으면 그것을 사용(실제 변동 반영), 없으면 단일 시점
                MarketItem item = row.Imported
                    ?? new MarketItem(name, new[] { new PricePoint(p.EndDate, cost) });

                items.Add(new ItemInput(item, margin, prevPrice));
            }
            return items;
        }

        private static decimal ParseDecimal(string text)
        {
            if (decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal v)
                || decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                return v < 0m ? 0m : v;
            return 0m;
        }

        /// <summary>계산 결과를 각 물품 행의 출력 라벨(시세/판매가/희망가)에 단위 변환하여 표시한다.</summary>
        private void WriteResults(CalculationResult result, Unit unit)
        {
            for (int i = 0; i < result.Items.Count && i < _rows.Count; i++)
            {
                var r = result.Items[i];
                _rows[i].MarketPrice.Text = unit.Format(r.MarketPrice);          // 시세 (세전)
                _rows[i].SalePrice.Text = unit.Format(r.SalePrice);              // 판매가 (개인 시세)
                _rows[i].RetailPrice.Text = unit.Format(r.SuggestedRetailPrice); // 희망소비자가격
            }
        }

        private void UpdateStability(CalculationResult result)
        {
            int pct = (int)Math.Round(result.Stability * 100.0);
            progressBar1.Value = Math.Max(progressBar1.Minimum, Math.Min(progressBar1.Maximum, pct));
            label29.Text = result.StabilityPercentText;
        }
    }
}
