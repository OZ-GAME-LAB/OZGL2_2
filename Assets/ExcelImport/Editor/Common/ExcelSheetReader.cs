using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

// 시트의 셀을 문자열로 읽음. 데이터별 검증과 S.O 수정은 하지 않음
public static class ExcelSheetReader
{
    public class Row
    {
        public int Number;
        public Dictionary<string, string> Cells = new Dictionary<string, string>();
    }
    public static bool TryRead(string path, string sheetName, out List<Row> rows, out string error)
    {
        rows = new List<Row>();
        error = "";
        try
        {
            // 엑셀에서 파일을 열어둔 상태에서도 저장된 내용을 읽을 수 있도록 공유 허용
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XElement sheet = ReadXml(zip, "xl/workbook.xml").Descendants(ns + "sheet")
                    .FirstOrDefault(item => (string)item.Attribute("name") == sheetName);
                if (sheet == null)
                {
                    error = sheetName + " 시트가 없습니다.";
                    return false;
                }

                string id = (string)sheet.Attribute(rel + "id");
                XElement relationship = ReadXml(zip, "xl/_rels/workbook.xml.rels").Root.Elements()
                    .FirstOrDefault(item => (string)item.Attribute("Id") == id);
                if (relationship == null || (string)relationship.Attribute("TargetMode") == "External")
                {
                    error = "시트 연결 정보를 확인하세요.";
                    return false;
                }

                string target = (string)relationship.Attribute("Target");
                string sheetPath = new Uri(new Uri("http://xlsx/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                List<string> strings = new List<string>();
                if (zip.GetEntry("xl/sharedStrings.xml") != null)
                {
                    foreach (XElement item in ReadXml(zip, "xl/sharedStrings.xml").Descendants(ns + "si"))
                    {
                        strings.Add(string.Concat(item.Descendants(ns + "t").Select(text => text.Value)));
                    }
                }

                XDocument document = ReadXml(zip, sheetPath);
                if (document.Descendants(ns + "mergeCell").Any())
                {
                    error = sheetName + ": 병합 셀을 해제하세요.";
                    return false;
                }

                foreach (XElement row in document.Descendants(ns + "row"))
                {
                    string number = (string)row.Attribute("r");
                    Dictionary<string, string> cells = new Dictionary<string, string>();
                    foreach (XElement cell in row.Elements(ns + "c"))
                    {
                        string address = (string)cell.Attribute("r") ?? "";
                        if (cell.Element(ns + "f") != null)
                        {
                            error = $"{sheetName}!{address}: 수식 대신 계산된 값을 입력하세요.";
                            return false;
                        }
                        string type = (string)cell.Attribute("t");
                        string value = (string)cell.Element(ns + "v") ?? "";
                        if (type == "s")
                        {
                            value = strings[int.Parse(value, CultureInfo.InvariantCulture)];
                        }
                        else if (type == "inlineStr")
                        {
                            value = string.Concat(cell.Descendants(ns + "t").Select(text => text.Value));
                        }
                        else if (type != null && type != "n" && value.Length > 0)
                        {
                            error = $"{sheetName}!{address}: 숫자 또는 텍스트 셀을 사용하세요.";
                            return false;
                        }
                        string column = new string(address.TakeWhile(char.IsLetter).ToArray());
                        cells[column] = value.Trim();
                    }

                    if (!cells.Values.All(string.IsNullOrWhiteSpace))
                    {
                        rows.Add(new Row { Number = int.Parse(number, CultureInfo.InvariantCulture), Cells = cells });
                    }
                }                return true;
            }
        }
        catch (Exception exception)
        {
            error = "엑셀을 읽지 못했습니다. 파일 형식과 셀 값을 확인하세요. " + exception.Message;
            return false;
        }
    }

    private static XDocument ReadXml(ZipArchive zip, string path)
    {
        using (Stream stream = zip.GetEntry(path).Open())
        {
            return XDocument.Load(stream);
        }
    }
}
