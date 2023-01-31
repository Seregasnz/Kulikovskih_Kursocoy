using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Core;
using System.Windows.Media;
using System.Windows.Documents;
using System.Threading;
using System.Diagnostics;

namespace main
{
    public partial class MainWindow : Window
    {
        private int last_page = 1; //Переменная, отвечающая за последнюю страницу у выбранной категории
        private int current_page = 1; //Переменная, отвечающая за текущую страницу
        private int current_item = 0; //Переменная отвечающая за текущий предмет (по факту индекс в массиве)
        Dictionary<int, List<List<string>>> items_of_current_page = new Dictionary<int, List<List<string>>>(); //Поскольку теперь мы парсим сразу все страницы, сделаем словарь с предметами по страницам
        private bool app_enabled = false; //Переменная, отвечающаю за то, включили ли мы приложение или нет
        private List<string> headers = new List<string>() //Заголовки для запросов
        {
            "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0",
            "Accept: */*",
        };

        public MainWindow()
        {
            InitializeComponent();
        }
        private void CloseAnyExcelProcess()
        {
            foreach(var process in Process.GetProcesses())
            {
                if (process.ProcessName == "EXCEL")
                    process.Kill();
            }
        }
        private async void SelectCategoryButton_click(object sender, RoutedEventArgs e) //Метод обработки события нажатия на одну из 6 кнопок с категориями
        {
            last_page = 1; //Обнуляем последнюю страницу
            current_page = 1; //Обнуляем текущую страницу
            current_item = 0; //Обнуляем текущий элемент
            items_of_current_page = new Dictionary<int, List<List<string>>>(); //Обнулим информацию о текущих предметах

            Current_Page.Content = "Страница: 1";

            string selected_category = (sender as Button).Content.ToString(); //Получаем имя нажатой кнопки

            Dictionary<string, string> urls = new Dictionary<string, string>() //Словарь с ссылками по именам категорий
            {
                {"Acer", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any&f=discount.any%2Crating.any%2Cacer"},
                {"Apple", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any%2Capple%2Casus&f=discount.any%2Crating.any%2Capple"},
                {"Huawei", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any&f=discount.any%2Crating.any%2Chuawei"},
                {"Lenovo", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any&f=discount.any%2Crating.any%2Clenovo"},
                {"MSI", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any&f=discount.any%2Crating.any%2Cmsi"},
                {"HP", "https://www.citilink.ru/catalog/noutbuki/?pf=discount.any%2Crating.any&f=discount.any%2Crating.any%2Chp"},
            };

            string url_to_selected_category = urls[selected_category]; //Получаем ссылку на текущий товар

            try //В этой оболочке try будет происходить парсинг входных данных (количество страниц товара), если что-то пойдёт не так (сервер отклонит запрос), то просто выйдем из метода
            {
                using (WebClient wc = new WebClient()) //Аналогичная работа обёртки, как в методе ParsePage
                {
                    foreach (var header in headers) wc.Headers.Add(header); //Добавим заголовки

                    //Получим html код первой страницы (для взятия информации о последней страницы)
                    string html = wc.DownloadString(url_to_selected_category);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    ///////////////////////////////////////////////////////////////////////////////

                    //Получим последнюю страницу
                    var pages = doc.DocumentNode.SelectNodes("//div[@class='PaginationWidget__wrapper-pagination']//a");
                    if (pages != null)
                    {
                        last_page = int.Parse(pages[pages.Count - 2].InnerText);
                    }
                    ///////////////////////////
                }
            }
            catch //Если что-то пошло не так
            {
                MessageBox.Show("Ошибка! Повторите позже.");
                return;
            }

            await GetInfoAboutPagesAsync(url_to_selected_category, last_page); //Запускаем асинхронный метод GetInfoAboutPagesAsync и ждём выполнения

            if (app_enabled == false)
            {
                #region HyperLinkLabelSection
                TextBlock HyperLinkLabel = new TextBlock();
                HyperLinkLabel.FontFamily = new FontFamily("Times New Roman");
                HyperLinkLabel.FontSize = 20;
                HyperLinkLabel.HorizontalAlignment = HorizontalAlignment.Center;
                HyperLinkLabel.VerticalAlignment = VerticalAlignment.Bottom;
                HyperLinkLabel.Margin = new Thickness(0, 0, 100, 15);
                HyperLinkLabel.Height = 37;
                HyperLinkLabel.Height = 80;

                Hyperlink link_to_selected_item = new Hyperlink();
                link_to_selected_item.Click += CopyItemLink_click;
                link_to_selected_item.Inlines.Add("Ссылка");

                HyperLinkLabel.Inlines.Add(link_to_selected_item);
                #endregion
                #region SaveLabelSection
                TextBlock SaveLabel = new TextBlock();
                SaveLabel.FontFamily = new FontFamily("Times New Roman");
                SaveLabel.FontSize = 20;
                SaveLabel.HorizontalAlignment = HorizontalAlignment.Center;
                SaveLabel.VerticalAlignment = VerticalAlignment.Bottom;
                SaveLabel.Margin = new Thickness(75, 0, 0, -5);
                SaveLabel.Height = 37;
                SaveLabel.Height = 100;

                Hyperlink save = new Hyperlink();
                save.Click += SaveInfoAboutSelectedItem_click;
                save.Inlines.Add("Сохранить");

                SaveLabel.Inlines.Add(save);
                #endregion

                MainGrid.Children.Add(HyperLinkLabel);
                MainGrid.Children.Add(SaveLabel);

                app_enabled = true;
            }

            SetCurrentItem(); //Зададим информацию о текущем товаре (самый первый товар первой страницы после парсинга)
        }
        private void SetCurrentItem() //Метод заполнения данными выбранный предмет
        {
            try
            {
                Item_Name.Content = items_of_current_page[current_page][current_item][0];
                Item_Price.Content = items_of_current_page[current_page][current_item][1];
                Item_Icon.Source = new BitmapImage(new Uri(items_of_current_page[current_page][current_item][3]));
            }
            catch
            {
                MessageBox.Show("Ошибка! Повторите позже!");
                return;
            }
        }
        private void ChangeCurrentItemBtn_click(object sender, RoutedEventArgs e)
        {
            string dir = (string)(sender as Button).Content;

            try
            {
                if (dir == ">")
                {
                    if (current_item == items_of_current_page[current_page].Count - 1)
                        current_item = 0;
                    else
                        current_item++;
                }
                else
                {
                    if (current_item == 0)
                        current_item = items_of_current_page[current_page].Count - 1;
                    else
                        current_item--;
                }

                SetCurrentItem();
            }

            catch
            {
                return;
            }
        } //Метод изменения текущего предмета
        private void ChangeCurrentPageBtn_click(object sender, RoutedEventArgs e)
        {
            string dir = (string)(sender as Button).Content;

            try
            {
                if (dir == ">")
                {
                    if (current_page == last_page)
                        current_page = 1;
                    else
                        current_page++;
                }
                else
                {
                    if (current_page == 1)
                        current_page = last_page;
                    else
                        current_page--;
                }

                current_item = 0;
                Current_Page.Content = $"Страница: {current_page}";

                SetCurrentItem();
            }

            catch
            {
                return;
            }
        } //Метод изменения текущей страницы
        private async Task GetInfoAboutPagesAsync(string url, int pages) //Асинхронный метод вызова задач парсинга страниц
        {
            for (int page = 1; page <= pages; page++) //Циклом проходим по всем страницам в нашем промежутке
            {
                string url_to_current_page = url + $"&p={page}"; //Получаем ссылку на текущую страницу
                await Task.Run(() => ParsePage(url_to_current_page, page)); //Запускаем асинхронно нашу задачу парсинга страницы и ждем выполнения 
            }

            //Как аналог - можно создать список задач, а затем с помощью Task.WhenAll асинхронно вызвать все задачи
        }
        private void ParsePage(string url, int current_page) //Метод парсинга страницы
        {
            List<List<string>> Items = new List<List<string>>(); //Создадим список текущей страницы

            try //В этом блоке try происходит парсинг
            {
                using (WebClient wc = new WebClient()) //Оборачиваем экземпляр класса WebClient для правильного использования методов
                {
                    foreach (var header in headers) wc.Headers.Add(header); //Добавим заголовки

                    //Загрузим наш html код страницы в HtmlDocument
                    string html = wc.DownloadString(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    ///////////////////////////////////////////////

                    //Получим div контейнеры всех предметов на странице
                    var items = doc.DocumentNode.SelectNodes("//section[@class='GroupGrid js--GroupGrid GroupGrid_has-column-gap GroupGrid_has-row-gap GroupGrid_has-slider']//div[@class='product_data__gtm-js product_data__pageevents-js  ProductCardVertical js--ProductCardInListing ProductCardVertical_normal ProductCardVertical_shadow-hover ProductCardVertical_separated']");
                    if(items == null)
                    {
                        MessageBox.Show("Ошибка! Повторите позже!");
                        return;
                    }

                    //Проидёмся циклом foreach по каждому предмету
                    foreach (var item in items)
                    {
                        List<string> current_items = new List<string>(); //Создадим список параметров текущего предмета

                        //Получим html код текущего предмета
                        string item_html = item.InnerHtml;
                        doc = new HtmlDocument();
                        doc.LoadHtml(item_html);
                        ////////////////////////////////////

                        //Форматирование и парсинг данных
                        string item_name = doc.DocumentNode.SelectSingleNode("//a[@class=' ProductCardVertical__name  Link js--Link Link_type_default']").InnerText;
                        item_name = item_name.Substring(14, item_name.IndexOf(",") - 14);
                        string item_price = doc.DocumentNode.SelectSingleNode("//span[@class='ProductCardVerticalPrice__price-current_current-price js--ProductCardVerticalPrice__price-current_current-price ']").InnerText;
                        item_price = item_price.Replace("\n", "");
                        item_price = item_price.Replace(" ", "") + " ₽";

                        string item_link = "https://www.citilink.ru" + doc.DocumentNode.SelectSingleNode("//a[@class=' ProductCardVertical__link link_gtm-js  Link js--Link Link_type_default']").Attributes[1].Value;
                        string item_icon = doc.DocumentNode.SelectNodes("//div[@class='ProductCardVertical__picture-hover_part js--ProductCardInListing__picture-hover_part']")[0].Attributes[1].Value;

                        current_items.Add(item_name);
                        current_items.Add(item_price);
                        current_items.Add(item_link);
                        current_items.Add(item_icon);
                        ///////////////////////////////

                        Items.Add(current_items); //Добавим текущий список параметров предмета в общий список предметов страницы
                    }           
                }
                items_of_current_page[current_page] = Items; //Зададим значение: для текущей страницы - текущие предметы (параметры/атрибуты предметов)
            }
            catch //Если что-то пошло не так
            {
                MessageBox.Show("Произошла ошибка, повторите позже!");
                return;
            }
        }
        private void CopyItemLink_click(object sender, RoutedEventArgs e) //Метод копирования ссылки
        {
            try
            {
                Clipboard.SetText(items_of_current_page[current_page][current_item][2]);
                MessageBox.Show("Ссылка скопирована в буфер обмена!");
            }
            catch
            {
                return;
            }
        }
        private void SaveInfoAboutSelectedItem_click(object sender, RoutedEventArgs e) //Метод сохранения информации в Excel
        {
            bool file_contain = false;

            foreach (var file in new DirectoryInfo(Environment.CurrentDirectory + @"\src\").GetFiles())
            {
                if (file.Name == "file.xlsx")
                {
                    file_contain = true;
                    break;
                }
            }

            if (file_contain)
            {
                CloseAnyExcelProcess(); //Закроем все процессы Excel

                try //В этом блоке try скачаем картинку по ссылке
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(items_of_current_page[current_page][current_item][3], Environment.CurrentDirectory + @"\src\img.jpg");
                    }
                }
                catch
                {
                    MessageBox.Show("Ошибка, повторите позже");
                    return;
                }

                             //Открываем Excel документ
                        Excel.Application ExcelApp = new Excel.Application();
                        ExcelApp.Visible = false;
                var retry = true;
                do
                {
                    try
                    {

                        Excel.Workbook Workbook = ExcelApp.Workbooks.Open(Environment.CurrentDirectory + @"\src\file.xlsx");
                        Excel.Worksheet worksheet = Workbook.ActiveSheet;

                        Excel.Range last = worksheet.Cells.SpecialCells(Excel.XlCellType.xlCellTypeLastCell, Type.Missing); //Получаем последний элемент в виде строка, столбец

                        int row = last.Row == 1 ? last.Row + 1 : last.Row + 6; //Считаем последнюю строку (заполненную) и переходим к следующей
                        int margin_top = 20 + (row / 6 * 90); //Формула для подсчета отсупа сверху для картинки

                        worksheet.Shapes.AddPicture(Environment.CurrentDirectory + @"\src\img.jpg", MsoTriState.msoFalse, MsoTriState.msoCTrue, 0, margin_top, 120, 80); //Добавляем картинку

                        //Проходим по каждому из столбцов (a, b, c, d), объединяем ячейки и заполняем их данными
                        foreach (var let in new List<string>() { "A", "B", "C", "D" })
                        {
                            Excel.Range range = worksheet.get_Range($"{let}{row}", $"{let}{row + 5}").Cells;
                            range.Merge(Type.Missing);

                            switch (let)
                            {
                                case "B":
                                    worksheet.Cells[row, 2] = items_of_current_page[current_page][current_item][0];
                                    break;
                                case "C":
                                    worksheet.Cells[row, 3] = items_of_current_page[current_page][current_item][1];
                                    break;
                                case "D":
                                    range.Formula = $@"=HYPERLINK(""{items_of_current_page[current_page][current_item][2]}"")";
                                    break;
                            }
                        }

                        //Сохраняем информацию в документе и закрываем его
                        Workbook.Save();
                        Workbook.Close(true);
                        ExcelApp.Quit();

                        MessageBox.Show("Информация сохранена");
                        File.Delete(Environment.CurrentDirectory + @"\src\img.jpg");

                        retry = false;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                } while (retry);
            }
            else
            {
                //Создадим новый объект Excel.Application
                Excel.Application ExcelApp = new Excel.Application();
                ExcelApp.Visible = false;

                object empty = System.Reflection.Missing.Value; //Специальный объект в виде никакущего значения, нужен для заполнения некоторых параметров при сохранении Excel документа

                Excel.Workbook Workbook = ExcelApp.Workbooks.Add(empty);
                Excel.Worksheet worksheet = Workbook.ActiveSheet;

                worksheet.Cells[1, 1] = "Изображение";
                worksheet.Cells[1, 2] = "Название";
                worksheet.Cells[1, 3] = "Цена";
                worksheet.Cells[1, 4] = "Ссылка";

                //Создадим словарь длин ячеек
                Dictionary<string, int> column_widths = new Dictionary<string, int>()
                {
                    {"A", 23},
                    {"B", 35},
                    {"C", 15},
                    {"D", 65},
                };

                //Пройдёмся по каждой из букв столбцов и поменяем некоторые параметры (ширину ячейки и выравнивание)
                foreach (var let in new List<string>() { "A", "B", "C", "D" })
                {
                    Excel.Range range = worksheet.Columns[let];
                    range.ColumnWidth = column_widths[let];
                    range.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    range.VerticalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }

                //Сохраним Excel документ и выйдем
                Workbook.SaveAs(Environment.CurrentDirectory + @"\src\file.xlsx", Excel.XlFileFormat.xlOpenXMLWorkbook, empty, empty, empty, empty, Excel.XlSaveAsAccessMode.xlExclusive, empty, empty, empty, empty, empty);
                Workbook.Close(true);
                ExcelApp.Quit();

                MessageBox.Show($"Файл для сохранения создан успешно по пути:\n{Environment.CurrentDirectory + @"\src\file.xlsx"}\nДля сохранения нажмите ещё раз");
            }
        }
    }
}