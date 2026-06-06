using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MarketCalc.Core;

namespace WindowsFormsApp1
{
    /// <summary>코드로 구성하는 간단한 한 줄 입력 대화상자(디자이너 불필요).</summary>
    internal static class TextPrompt
    {
        /// <summary>입력값을 반환하거나, 취소 시 null.</summary>
        public static string Show(IWin32Window owner, string title, string prompt, string defaultValue = "")
        {
            using (var form = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 110);

                label.SetBounds(12, 15, 296, 20);
                label.Text = prompt;

                textBox.SetBounds(12, 40, 296, 25);
                textBox.Text = defaultValue;

                ok.SetBounds(140, 75, 80, 26);
                ok.Text = "확인";
                ok.DialogResult = DialogResult.OK;

                cancel.SetBounds(228, 75, 80, 26);
                cancel.Text = "취소";
                cancel.DialogResult = DialogResult.Cancel;

                form.Controls.AddRange(new Control[] { label, textBox, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }
    }

    /// <summary>단위(이름·기호·환산계수)를 추가하는 세부 설정 대화상자.</summary>
    internal static class UnitDialog
    {
        /// <summary>새 단위를 반환하거나, 취소/오류 시 null.</summary>
        public static Unit Show(IWin32Window owner)
        {
            using (var form = new Form())
            {
                form.Text = "단위 추가";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 170);

                var nameLabel = new Label { Text = "이름 (예: 파운드)", Bounds = new Rectangle(12, 12, 140, 20) };
                var nameBox = new TextBox { Bounds = new Rectangle(150, 10, 158, 25) };

                var symbolLabel = new Label { Text = "표기 (예: £)", Bounds = new Rectangle(12, 45, 140, 20) };
                var symbolBox = new TextBox { Bounds = new Rectangle(150, 43, 158, 25) };

                var rateLabel = new Label { Text = "환산계수 (원 1당)", Bounds = new Rectangle(12, 78, 140, 20) };
                var rateBox = new TextBox { Bounds = new Rectangle(150, 76, 158, 25), Text = "1" };

                var placesLabel = new Label { Text = "소수점 자리", Bounds = new Rectangle(12, 111, 140, 20) };
                var placesBox = new TextBox { Bounds = new Rectangle(150, 109, 158, 25), Text = "0" };

                var ok = new Button { Text = "추가", Bounds = new Rectangle(140, 165, 80, 26), DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "취소", Bounds = new Rectangle(228, 165, 80, 26), DialogResult = DialogResult.Cancel };

                form.ClientSize = new Size(320, 205);
                form.Controls.AddRange(new Control[]
                {
                    nameLabel, nameBox, symbolLabel, symbolBox, rateLabel, rateBox, placesLabel, placesBox, ok, cancel
                });
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                if (form.ShowDialog(owner) != DialogResult.OK)
                    return null;

                string name = nameBox.Text.Trim();
                string symbol = symbolBox.Text.Trim();
                if (!decimal.TryParse(rateBox.Text.Trim(), out decimal rate) || rate <= 0m)
                {
                    MessageBox.Show("환산계수는 0보다 큰 숫자여야 합니다.", "입력 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                if (!int.TryParse(placesBox.Text.Trim(), out int places) || places < 0 || places > 6)
                {
                    MessageBox.Show("소수점 자리는 0~6 사이의 정수여야 합니다.", "입력 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                try
                {
                    return new Unit(name, symbol, rate, places);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// 고/중/저 소득의 소득액·인구비율을 입력받아 인구 비율 가중평균으로
    /// 평균소득을 산출하는 세부 설정 대화상자.
    /// </summary>
    internal static class IncomeDialog
    {
        /// <summary>(평균소득, 기준소득)을 반환하거나, 취소 시 null.</summary>
        public static (decimal average, decimal reference)? Show(IWin32Window owner)
        {
            using (var form = new Form())
            {
                form.Text = "평균소득 세부 설정";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(360, 290);

                new Label { Text = "계층", Bounds = new Rectangle(12, 12, 90, 20), Parent = form };
                new Label { Text = "소득액(원)", Bounds = new Rectangle(110, 12, 110, 20), Parent = form };
                new Label { Text = "인구비율(%)", Bounds = new Rectangle(230, 12, 110, 20), Parent = form };

                var rows = new (string name, decimal income, decimal share)[]
                {
                    ("고소득", 6000000m, 20m),
                    ("중위소득", 3000000m, 50m),
                    ("저소득", 1500000m, 30m),
                };

                var incomeBoxes = new TextBox[3];
                var shareBoxes = new TextBox[3];
                for (int i = 0; i < 3; i++)
                {
                    int y = 40 + i * 34;
                    new Label { Text = rows[i].name, Bounds = new Rectangle(12, y + 3, 90, 20), Parent = form };
                    incomeBoxes[i] = new TextBox
                    {
                        Bounds = new Rectangle(110, y, 110, 25),
                        Text = rows[i].income.ToString(CultureInfo.InvariantCulture),
                        Parent = form
                    };
                    shareBoxes[i] = new TextBox
                    {
                        Bounds = new Rectangle(230, y, 110, 25),
                        Text = rows[i].share.ToString(CultureInfo.InvariantCulture),
                        Parent = form
                    };
                }

                var hint = new Label
                {
                    Text = "가중평균 = Σ(소득×인구비율) / Σ(인구비율)",
                    Bounds = new Rectangle(12, 150, 336, 20),
                    ForeColor = Color.Gray,
                    Parent = form
                };

                new Label { Text = "기준소득(소득계수 분모)", Bounds = new Rectangle(12, 178, 150, 20), Parent = form };
                var refBox = new TextBox
                {
                    Bounds = new Rectangle(170, 176, 110, 25),
                    Text = rows[1].income.ToString(CultureInfo.InvariantCulture), // 기본=중위소득
                    Parent = form
                };

                var ok = new Button { Text = "적용", Bounds = new Rectangle(180, 250, 80, 28), DialogResult = DialogResult.OK, Parent = form };
                var cancel = new Button { Text = "취소", Bounds = new Rectangle(268, 250, 80, 28), DialogResult = DialogResult.Cancel, Parent = form };
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                if (form.ShowDialog(owner) != DialogResult.OK)
                    return null;

                try
                {
                    var brackets = new IncomeBracket[3];
                    for (int i = 0; i < 3; i++)
                    {
                        decimal income = ParseNonNegative(incomeBoxes[i].Text, rows[i].name + " 소득액");
                        decimal share = ParseNonNegative(shareBoxes[i].Text, rows[i].name + " 인구비율");
                        brackets[i] = new IncomeBracket(rows[i].name, income, share);
                    }
                    decimal average = IncomeModel.WeightedAverage(brackets);
                    decimal reference = ParseNonNegative(refBox.Text, "기준소득");
                    if (reference <= 0m) reference = average; // 0이면 평균소득으로 대체
                    return (average, reference);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
            }
        }

        private static decimal ParseNonNegative(string text, string field)
        {
            if (!decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal v)
                && !decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                throw new FormatException($"{field}: 숫자를 입력하세요.");
            if (v < 0m) throw new FormatException($"{field}: 0 이상이어야 합니다.");
            return v;
        }
    }
}
