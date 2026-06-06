using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MarketCalc.Core;

namespace WindowsFormsApp1
{
    public partial class Form1
    {
        /// <summary>"가져오기": .txt에서 날짜별 생산비 이력을 읽어 테이블에 채운다.</summary>
        private void ImportData()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "생산비 이력 가져오기",
                Filter = "텍스트/CSV 파일 (*.txt;*.csv)|*.txt;*.csv|모든 파일 (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string text;
                try
                {
                    text = File.ReadAllText(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("파일을 읽을 수 없습니다: " + ex.Message, "가져오기 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var items = MarketDataFile.Parse(text);
                if (items.Count == 0)
                {
                    MessageBox.Show("읽을 수 있는 데이터가 없습니다.\n형식: 날짜,물품명,생산비 (예: 2024-01-01,쌀,1000)",
                        "가져오기", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ApplyImportedItems(items);
                MessageBox.Show($"{items.Count}개 물품, 총 {items.Sum(i => i.History.Count)}개 기록을 가져왔습니다.",
                    "가져오기 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>가져온 물품 수만큼 세로 리스트 행을 만들어 채운다(최대 MaxItems).</summary>
        private void ApplyImportedItems(System.Collections.Generic.IReadOnlyList<MarketItem> items)
        {
            DateTime? minDate = null, maxDate = null;

            int count = Math.Min(items.Count, MaxItems);
            _rows.Clear();
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                var row = CreateItemRow(item.Name);
                row.Imported = item;
                row.Cost.Text = item.History.Last().ProductionCost.ToString(CultureInfo.InvariantCulture);
                _rows.Add(row);

                var first = item.History.First().Date;
                var last = item.History.Last().Date;
                if (minDate == null || first < minDate) minDate = first;
                if (maxDate == null || last > maxDate) maxDate = last;
            }
            if (_rows.Count == 0) _rows.Add(CreateItemRow("물품1"));
            RebuildTable();

            // 가져온 데이터 기간으로 조회 범위 자동 설정
            if (minDate.HasValue) dateTimePicker2.Value = minDate.Value;
            if (maxDate.HasValue) dateTimePicker1.Value = maxDate.Value;
        }

        /// <summary>"내보내기": 마지막 계산 결과를 .txt 표로 저장한다.</summary>
        private void ExportData()
        {
            if (_lastResult == null)
            {
                MessageBox.Show("먼저 '확인'을 눌러 계산한 뒤 내보내기 하세요.", "내보내기",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = "계산 결과 내보내기",
                Filter = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv",
                FileName = "시세계산결과.txt"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string text = MarketDataFile.ExportResults(_lastResult, _lastUnit ?? Unit.Base);
                    File.WriteAllText(dlg.FileName, text);
                    MessageBox.Show("저장했습니다: " + dlg.FileName, "내보내기 완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("저장 실패: " + ex.Message, "내보내기 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
