using System;
using System.Windows.Forms;
using MarketCalc.Core;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        /// <summary>comboBox2에서 단위 추가를 트리거하는 센티넬 항목.</summary>
        private const string AddUnitSentinel = "＋ 단위 추가…";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitTaxRate();
            InitUnits();
            InitCalcRatios();
            InitQuantity();
            InitCriterionCheckBoxes();
            BuildEditableTable();
            BuildPriceTable();
            InitIncomeSettings();
            InitConsumptionSettings();
            InitVolatilityToggle();
            InitDateRange();
            InitFileButtons();
            InitResponsiveLayout();
        }

        // ---- 소비성향: 라벨 클릭으로 % 조정 (안정도 = 1 − |변동률 × 소비성향|) ----
        private void InitConsumptionSettings()
        {
            label7.Text = "소비성향 ⚙"; // 안정도 산정에 쓰임 (label8은 BuildPriceTable에서 캡션 지정)
            label7.Cursor = Cursors.Hand;
            label7.Click += (s, e) =>
            {
                string cur = MarketCalc.Core.Percent.Format(_consumptionPropensity);
                string input = TextPrompt.Show(this, "소비성향", "가처분소득 중 소비 비율(%) (예: 70)", cur);
                if (input == null) return;
                if (!MarketCalc.Core.Percent.TryParse(input, out decimal c))
                {
                    MessageBox.Show("0 이상의 퍼센트를 입력하세요.", "입력 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _consumptionPropensity = c;
            };
            var tip = new ToolTip();
            tip.SetToolTip(label7, "클릭하면 소비성향(%)을 조정합니다. 안정도 = 1 − |변동률 × 소비성향|: 소비↑일수록 같은 가격변동이 더 불안정.");
        }

        // ---- 변동 시뮬레이션 토글: label4 클릭으로 on/off ----
        private void InitVolatilityToggle()
        {
            label4.Cursor = Cursors.Hand;
            UpdateVolatilityLabel();
            label4.Click += (s, e) =>
            {
                _useVolatility = !_useVolatility;
                UpdateVolatilityLabel();
                if (_lastResult != null) RunCalculation(); // 즉시 반영
            };
            var tip = new ToolTip();
            tip.SetToolTip(label4,
                "켜면 사인+노이즈로 시세가 출렁입니다(수치·그래프 모두, 고정 시드로 재현 가능). " +
                "끄면 실제 생산비 이력에만 의존하는 결정적 값입니다.");
        }

        private void UpdateVolatilityLabel()
        {
            label4.Text = _useVolatility ? "변동 시뮬레이션: 켜짐 ●" : "변동 시뮬레이션: 꺼짐 ○";
            label4.ForeColor = _useVolatility
                ? System.Drawing.Color.SeaGreen
                : System.Drawing.Color.Gray;
        }

        // ---- 가져오기/내보내기 버튼 ----
        private void InitFileButtons()
        {
            button2.Click += (s, e) => ExportData(); // 내보내기
            button3.Click += (s, e) => ImportData(); // 가져오기
        }

        // (소매 마진율 조정은 테이블의 '희망소비자가격 ⚙' 행 제목 클릭으로 처리: EditRetailMargin)

        // ---- 반응형 레이아웃: 창 크기 변경 시 잘리지 않도록 ----
        private void InitResponsiveLayout()
        {
            // 디자인 크기보다 작아지지 않게 하고, 부족하면 스크롤로 대체 → 잘림 방지
            MinimumSize = Size;
            AutoScroll = true;

            // 상단 진행바는 가로로 늘어나도록
            progressBar1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // 우측 입력 묶음은 오른쪽에 고정(가로 확장 시 따라감)
            var rightAnchored = new Control[]
            {
                comboBox1, comboBox2, label1, label2, button1,
                numericUpDown1, label5
            };
            foreach (var c in rightAnchored)
                c.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // 물품 리스트 테이블은 세로로 늘어나며 넘치면 스크롤되도록 상·하·우 고정
            tableLayoutPanel1.Anchor =
                AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        }

        // ---- 기본 조회 기간: 최근 30일 (그래프가 바로 추이를 그리도록) ----
        private void InitDateRange()
        {
            dateTimePicker1.Value = DateTime.Today;              // 종료일
            dateTimePicker2.Value = DateTime.Today.AddDays(-30); // 시작일
        }

        // ---- 평균소득: 라벨 클릭 시 고/중/저 소득 세부 설정 ----
        private void InitIncomeSettings()
        {
            label3.Text = "평균 소득 ⚙";
            label3.Cursor = Cursors.Hand;
            label3.Click += (s, e) => OpenIncomeSettings();
            var tip = new ToolTip();
            tip.SetToolTip(label3, "클릭하면 고/중/저 소득으로 평균소득을 자동 계산합니다.");
        }

        // ---- 세율: 퍼센트 직접 입력 (편집 가능 콤보, 프리셋은 참고용) ----
        private void InitTaxRate()
        {
            comboBox1.DropDownStyle = ComboBoxStyle.DropDown; // 직접 입력 허용
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[] { "5%", "10%", "15%", "20%", "25%" });
            comboBox1.Text = "10%";
        }

        // ---- 단위: 7개국 프리셋 + 세부 설정으로 사용자 추가 ----
        private void InitUnits()
        {
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList; // 목록에서 선택
            comboBox2.Items.Clear();
            foreach (var unit in CurrencyUnits.Defaults)
                comboBox2.Items.Add(unit);
            comboBox2.Items.Add(AddUnitSentinel);
            comboBox2.SelectedIndex = 0;
            comboBox2.SelectedIndexChanged += comboBox2_SelectedIndexChanged;
        }

        // ---- 기본 마진율: 퍼센트 표기, 더블클릭으로 추가 / Delete로 삭제 ----
        private void InitCalcRatios()
        {
            label6.Text = "기본 마진율"; // 물품별 마진율이 비었을 때의 공통값
            listBox1.Items.Clear();
            listBox1.Items.AddRange(new object[] { "5%", "10%", "15%", "20%", "30%" });
            listBox1.SelectedIndex = 1;
            listBox1.DoubleClick += listBox1_DoubleClick;
            listBox1.KeyDown += listBox1_KeyDown;
        }

        private void InitQuantity()
        {
            numericUpDown1.Minimum = 1;
            numericUpDown1.Maximum = 100000;
            numericUpDown1.Value = 1;
        }

        // ---- 체크박스 3개를 독립 토글로 (여러 요소를 종합 결합) ----
        private void InitCriterionCheckBoxes()
        {
            // 상호 배타 아님: 켜진 요소들이 시세에 곱셈으로 결합된다.
            checkBox3.Checked = true; // 기본: 판매(마진) 요소
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(comboBox2.SelectedItem is string s) || s != AddUnitSentinel)
                return;

            var unit = UnitDialog.Show(this);
            int sentinelIndex = comboBox2.Items.IndexOf(AddUnitSentinel);
            if (unit != null)
            {
                comboBox2.Items.Insert(sentinelIndex, unit);
                comboBox2.SelectedItem = unit;
            }
            else
            {
                comboBox2.SelectedIndex = 0; // 취소 시 첫 단위로 복귀
            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            string input = TextPrompt.Show(this, "계산 비율 추가", "퍼센트 값을 입력하세요 (예: 12.5)");
            if (input == null) return;

            if (!Percent.TryParse(input, out decimal ratio))
            {
                MessageBox.Show("퍼센트 값으로 해석할 수 없습니다.", "입력 오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string formatted = Percent.Format(ratio);
            int existing = listBox1.Items.IndexOf(formatted);
            if (existing >= 0)
            {
                listBox1.SelectedIndex = existing;
            }
            else
            {
                listBox1.SelectedIndex = listBox1.Items.Add(formatted);
            }
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && listBox1.SelectedIndex >= 0 && listBox1.Items.Count > 1)
            {
                int idx = listBox1.SelectedIndex;
                listBox1.Items.RemoveAt(idx);
                listBox1.SelectedIndex = Math.Min(idx, listBox1.Items.Count - 1);
            }
        }

        // ---- 아래는 다음 단계에서 채울 핸들러 ----

        private void button1_Click(object sender, EventArgs e)
        {
            RunCalculation();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void folderBrowserDialog2_HelpRequest(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }
    }
}
