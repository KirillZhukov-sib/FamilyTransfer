using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyTransfer.Cache;
using RevitToMySQL;
using RevitToMySQL.Caching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FamilyTransfer.ViewModels
{
    public class ExportViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isExporting;
        private SectionCache _sectionCache;
        private readonly Regex _ovPattern;

        // Свойства для привязки к UI
        private string _documentTitle;
        private string _databaseName = "testovk";
        private string _statusMessage = "Готов к работе";
        private string _progressDetail = "Ожидание начала экспорта...";
        private string _speedInfo = "";
        private string _timeInfo = "";
        private string _logMessages = "";
        private double _progressValue;
        private double _progressMaximum = 100;
        private string _progressText = "0%";
        private bool _isIndeterminate;

        // Категории для выбора
        private bool _includePipeAccessory = true;
        private bool _includePipeFitting = true;
        private bool _includePlumbingFixtures = true;
        private bool _includePipeCurves = true;
        private bool _includePipeInsulations = true;

        private bool _includeDuctCurves = true;
        private bool _includeMechanicalEquipment = true;
        private bool _includeDuctTerminal = true;
        private bool _includeDuctAccessory = true;
        private bool _includeDuctInsulations = true;
        private bool _includeDuctFitting = true;

        // Свойства для связей
        private ObservableCollection<string> _loadedLinks = new ObservableCollection<string>();
        private string _linksCountInfo = "Связи не найдены";
        private bool _includeLinkedModels = true;

        // Свойства для категорий
        public bool IncludePipeAccessory
        {
            get => _includePipeAccessory;
            set { _includePipeAccessory = value; OnPropertyChanged(); }
        }
        public bool IncludePipeFitting
        {
            get => _includePipeFitting;
            set { _includePipeFitting = value; OnPropertyChanged(); }
        }
        public bool IncludePlumbingFixtures
        {
            get => _includePlumbingFixtures;
            set { _includePlumbingFixtures = value; OnPropertyChanged(); }
        }
        public bool IncludePipeCurves
        {
            get => _includePipeCurves;
            set { _includePipeCurves = value; OnPropertyChanged(); }
        }
        public bool IncludePipeInsulations
        {
            get => _includePipeInsulations;
            set { _includePipeInsulations = value; OnPropertyChanged(); }
        }
        public bool IncludeDuctCurves
        {
            get => _includeDuctCurves;
            set { _includeDuctCurves = value; OnPropertyChanged(); }
        }
        public bool IncludeMechanicalEquipment
        {
            get => _includeMechanicalEquipment;
            set { _includeMechanicalEquipment = value; OnPropertyChanged(); }
        }
        public bool IncludeDuctTerminal
        {
            get => _includeDuctTerminal;
            set { _includeDuctTerminal = value; OnPropertyChanged(); }
        }
        public bool IncludeDuctAccessory
        {
            get => _includeDuctAccessory;
            set { _includeDuctAccessory = value; OnPropertyChanged(); }
        }
        public bool IncludeDuctInsulations
        {
            get => _includeDuctInsulations;
            set { _includeDuctInsulations = value; OnPropertyChanged(); }
        }
        public bool IncludeDuctFitting
        {
            get => _includeDuctFitting;
            set { _includeDuctFitting = value; OnPropertyChanged(); }
        }

        // Свойства для связей
        public ObservableCollection<string> LoadedLinks
        {
            get => _loadedLinks;
            set { _loadedLinks = value; OnPropertyChanged(); }
        }

        public string LinksCountInfo
        {
            get => _linksCountInfo;
            set { _linksCountInfo = value; OnPropertyChanged(); }
        }

        public bool IncludeLinkedModels
        {
            get => _includeLinkedModels;
            set { _includeLinkedModels = value; OnPropertyChanged(); }
        }

        // Остальные свойства
        public string DocumentTitle
        {
            get => _documentTitle;
            set { _documentTitle = value; OnPropertyChanged(); }
        }

        public string DatabaseName
        {
            get => _databaseName;
            set { _databaseName = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string ProgressDetail
        {
            get => _progressDetail;
            set { _progressDetail = value; OnPropertyChanged(); }
        }

        public string SpeedInfo
        {
            get => _speedInfo;
            set { _speedInfo = value; OnPropertyChanged(); }
        }

        public string TimeInfo
        {
            get => _timeInfo;
            set { _timeInfo = value; OnPropertyChanged(); }
        }

        public string LogMessages
        {
            get => _logMessages;
            set { _logMessages = value; OnPropertyChanged(); }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public double ProgressMaximum
        {
            get => _progressMaximum;
            set { _progressMaximum = value; OnPropertyChanged(); }
        }

        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        public bool CanStart => !_isExporting;
        public bool CanStop => _isExporting;

        // Команды
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SelectAllPipesCommand { get; }
        public ICommand DeselectAllPipesCommand { get; }
        public ICommand SelectAllDuctsCommand { get; }
        public ICommand DeselectAllDuctsCommand { get; }

        public ExportViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _doc = uiDoc.Document ?? throw new ArgumentNullException(nameof(uiDoc.Document));

            _ovPattern = new Regex(@"^.*-.*-.*-.*-ОВ-.*$", RegexOptions.IgnoreCase);

            DocumentTitle = $"Текущая модель: {_doc.Title}";

            StartCommand = new RelayCommand(async () => await StartExportAsync(), () => CanStart);
            StopCommand = new RelayCommand(StopExport, () => CanStop);

            SelectAllPipesCommand = new RelayCommand(SelectAllPipes);
            DeselectAllPipesCommand = new RelayCommand(DeselectAllPipes);
            SelectAllDuctsCommand = new RelayCommand(SelectAllDucts);
            DeselectAllDuctsCommand = new RelayCommand(DeselectAllDucts);

            LoadLinks();
            LoadSections();
        }

        // ========== ЗАГРУЗКА СВЯЗЕЙ ==========

        private void LoadLinks()
        {
            try
            {
                LogMessage("========================================");
                LogMessage(" ПОИСК ПОДГРУЖЕННЫХ СВЯЗЕЙ С ОВ");
                LogMessage("========================================");

                LoadedLinks.Clear();

                var allLinks = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                LogMessage($"Найдено связей всего: {allLinks.Count}");

                var filteredLinks = new List<RevitLinkInstance>();

                if (allLinks.Count > 0)
                {
                    foreach (var link in allLinks)
                    {
                        string fullName = link.Name;
                        string displayName = fullName.Contains(":") ? fullName.Split(':')[0].Trim() : fullName;

                        if (_ovPattern.IsMatch(displayName))
                        {
                            filteredLinks.Add(link);
                            LoadedLinks.Add(displayName);
                            LogMessage($"   {displayName} - добавлена");
                        }
                        else
                        {
                            LogMessage($"   {displayName} - пропущена (не содержит ОВ)");
                        }
                    }

                    LinksCountInfo = $" Найдено связей с ОВ: {filteredLinks.Count} из {allLinks.Count}";
                }
                else
                {
                    LinksCountInfo = " Нет подгруженных связей";
                    LogMessage(" ВНИМАНИЕ: Нет подгруженных связей!");
                }

                LogMessage("========================================");
                LogMessage(" ПОИСК СВЯЗЕЙ ЗАВЕРШЕН");
                LogMessage("========================================");
            }
            catch (Exception ex)
            {
                LogMessage($" ОШИБКА загрузки связей: {ex.Message}");
                LinksCountInfo = " Ошибка загрузки связей";
            }
        }

        // ========== ЗАГРУЗКА СЕКЦИЙ ==========

        private void LoadSections()
        {
            try
            {
                _sectionCache = new SectionCache(_doc);
                LogMessage("Кэш секций создан");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ ОШИБКА загрузки секций: {ex.Message}");
            }
        }

        // ========== КОМАНДЫ ДЛЯ КАТЕГОРИЙ ==========

        private void SelectAllPipes()
        {
            IncludePipeAccessory = true;
            IncludePipeFitting = true;
            IncludePlumbingFixtures = true;
            IncludePipeCurves = true;
            IncludePipeInsulations = true;
            LogMessage("Выбраны все категории труб");
        }
        private void DeselectAllPipes()
        {
            IncludePipeAccessory = false;
            IncludePipeFitting = false;
            IncludePlumbingFixtures = false;
            IncludePipeCurves = false;
            IncludePipeInsulations = false;
            LogMessage("Сняты все категории труб");
        }
        private void SelectAllDucts()
        {
            IncludeDuctCurves = true;
            IncludeMechanicalEquipment = true;
            IncludeDuctTerminal = true;
            IncludeDuctAccessory = true;
            IncludeDuctInsulations = true;
            IncludeDuctFitting = true;
            LogMessage("Выбраны все категории воздуховодов");
        }
        private void DeselectAllDucts()
        {
            IncludeDuctCurves = false;
            IncludeMechanicalEquipment = false;
            IncludeDuctTerminal = false;
            IncludeDuctAccessory = false;
            IncludeDuctInsulations = false;
            IncludeDuctFitting = false;
            LogMessage("Сняты все категории воздуховодов");
        }

        // ========== ПОЛУЧЕНИЕ ВСЕХ ДОКУМЕНТОВ ==========
        private List<Document> GetAllDocuments()
        {
            var result = new List<Document>();

            result.Add(_doc);

            if (IncludeLinkedModels)
            {
                var allLinks = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in allLinks)
                {
                    try
                    {
                        string fullName = link.Name;
                        string displayName = fullName.Contains(":") ? fullName.Split(':')[0].Trim() : fullName;

                        if (_ovPattern.IsMatch(displayName))
                        {
                            Document linkDoc = link.GetLinkDocument();
                            if (linkDoc != null)
                            {
                                result.Add(linkDoc);
                                LogMessage($"Добавлена связь с ОВ: {link.Name}");
                            }
                            else
                            {
                                LogMessage($" Связь с ОВ не загружена: {link.Name}");
                            }
                        }
                        else
                        {
                            LogMessage($" Связь без ОВ пропущена: {link.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($" Ошибка доступа к связи: {ex.Message}");
                    }
                }
            }

            return result;
        }

        // ========== СБОР ЭЛЕМЕНТОВ ==========

        private List<Element> GetPipeElementsBySelection(Document doc)
        {
            var filters = new List<ElementFilter>();

            if (IncludePipeAccessory)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory));
            if (IncludePipeFitting)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting));
            if (IncludePlumbingFixtures)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures));
            if (IncludePipeCurves)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves));
            if (IncludePipeInsulations)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations));

            if (filters.Count == 0)
                return new List<Element>();

            return new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(filters))
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .OrderBy(x => x.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsValueString() ?? "")
                .ToList();
        }

        private List<Element> GetDuctElementsBySelection(Document doc)
        {
            var filters = new List<ElementFilter>();

            if (IncludeDuctCurves)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves));
            if (IncludeMechanicalEquipment)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment));
            if (IncludeDuctTerminal)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_DuctTerminal));
            if (IncludeDuctAccessory)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory));
            if (IncludeDuctInsulations)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_DuctInsulations));
            if (IncludeDuctFitting)
                filters.Add(new ElementCategoryFilter(BuiltInCategory.OST_DuctFitting));

            if (filters.Count == 0)
                return new List<Element>();

            return new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(filters))
                .WhereElementIsNotElementType()
                .ToList();
        }

        // ========== ОСНОВНЫЕ МЕТОДЫ ЭКСПОРТА ==========

        private async Task StartExportAsync()
        {
            if (_isExporting) return;

            _isExporting = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));

            LogMessage("========== НАЧАЛО ЭКСПОРТА ==========");
            LogMessage($"База данных: {DatabaseName}");
            LogMessage($"Включая связанные модели: {IncludeLinkedModels}");
            LogMessage($"Выбранные категории:");
            LogMessage($"  Трубы: PipeAccessory={IncludePipeAccessory}, PipeFitting={IncludePipeFitting}, PlumbingFixtures={IncludePlumbingFixtures}, PipeCurves={IncludePipeCurves}, PipeInsulations={IncludePipeInsulations}");
            LogMessage($"  Воздуховоды: DuctCurves={IncludeDuctCurves}, MechanicalEquipment={IncludeMechanicalEquipment}, DuctTerminal={IncludeDuctTerminal}, DuctAccessory={IncludeDuctAccessory}, DuctInsulations={IncludeDuctInsulations}, DuctFitting={IncludeDuctFitting}");

            StatusMessage = "Экспорт в процессе...";
            IsIndeterminate = true;
            ProgressValue = 0;
            ProgressText = "0%";

            var stopwatchTotal = Stopwatch.StartNew();

            try
            {
                await Task.Run(() => ExportData(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                stopwatchTotal.Stop();
                LogMessage($"Общее время экспорта: {stopwatchTotal.Elapsed.TotalSeconds:F2} сек");
            }
            catch (OperationCanceledException)
            {
                stopwatchTotal.Stop();
                LogMessage("Экспорт прерван пользователем");
                StatusMessage = "Экспорт прерван";

                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Экспорт был прерван пользователем", "Информация",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                stopwatchTotal.Stop();
                LogMessage($"ОШИБКА: {ex.Message}");
                StatusMessage = "Ошибка экспорта";

                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Ошибка при экспорте:\n{ex.Message}", "Ошибка",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
            finally
            {
                _isExporting = false;
                IsIndeterminate = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));

                if (_cancellationTokenSource?.IsCancellationRequested != true)
                {
                    StatusMessage = "Экспорт завершен";
                    LogMessage("========== ЭКСПОРТ ЗАВЕРШЕН ==========");
                }
            }
        }

        private void StopExport()
        {
            LogMessage("ЗАПРОС НА ОСТАНОВКУ ЭКСПОРТА...");
            _cancellationTokenSource?.Cancel();
        }

        // ========== ОСНОВНОЙ МЕТОД ЭКСПОРТА ДАННЫХ ==========

        private async Task ExportData(CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // ========== 1. ПОЛУЧАЕМ ВСЕ ДОКУМЕНТЫ ==========
                List<Document> allDocs = GetAllDocuments();
                LogMessage($"Найдено документов для экспорта: {allDocs.Count}");

                if (allDocs.Count == 0)
                {
                    LogMessage(" Нет документов для экспорта!");
                    UpdateProgress(100, "Нет документов", "Нет документов для экспорта");
                    return;
                }

                // ========== 2. WIP CODE (из основной модели) ==========
                string wipCode = _doc.Title.Length >= 4 ? _doc.Title.Substring(0, 4) : _doc.Title;
                LogMessage($"WipCode: {wipCode}");

                // ========== 3. ИНИЦИАЛИЗАЦИЯ КЭШЕЙ ==========
                var typeCaches = new Dictionary<Document, ITypeParameterCacheProvider>();
                foreach (var doc in allDocs)
                {
                    typeCaches[doc] = new TypeParameterCacheProvider(doc);
                    LogMessage($"  Кэш типов для: {doc.Title}");
                }

                if (_sectionCache == null)
                {
                    _sectionCache = new SectionCache(_doc);
                }
                LogMessage($"Кэш секций создан");

                DatabaseCache dbCache = DatabaseCache.LoadAllData();
                LogMessage("Кэш из БД загружен");

                // ========== 4. СБОР ЭЛЕМЕНТОВ ==========
                UpdateProgress(20, "Сбор элементов...", "Поиск труб и воздуховодов");
                cancellationToken.ThrowIfCancellationRequested();

                var allPipeElements = new List<Element>();
                var allDuctElements = new List<Element>();

                foreach (var doc in allDocs)
                {
                    var pipes = GetPipeElementsBySelection(doc);
                    var ducts = GetDuctElementsBySelection(doc);

                    allPipeElements.AddRange(pipes);
                    allDuctElements.AddRange(ducts);

                    LogMessage($"  {doc.Title}: трубы - {pipes.Count}, воздуховоды - {ducts.Count}");
                }

                LogMessage($"ИТОГО элементов: трубы - {allPipeElements.Count}, воздуховоды - {allDuctElements.Count}");

                if (allPipeElements.Count == 0 && allDuctElements.Count == 0)
                {
                    LogMessage("❌ Не найдено элементов для экспорта!");
                    UpdateProgress(100, "Нет элементов", "Нет элементов для экспорта");
                    return;
                }

                // ========== 5. ОБРАБОТКА ПАРАМЕТРОВ ==========
                UpdateProgress(35, "Обработка параметров труб...", $"Обработка {allPipeElements.Count} трубных элементов");
                cancellationToken.ThrowIfCancellationRequested();

                var pipeData = new List<FamilyInfo>();
                var ductData = new List<FamilyInfo>();

                if (allPipeElements.Count > 0)
                {
                    pipeData = ParameterProcessor.GetParameters(
                        allPipeElements,
                        _doc,
                        dbCache,
                        _sectionCache,
                        typeCaches,
                        wipCode);
                    LogMessage($"Обработано трубных элементов: {pipeData.Count}");
                }

                UpdateProgress(50, "Обработка параметров воздуховодов...", $"Обработка {allDuctElements.Count} вентиляционных элементов");
                cancellationToken.ThrowIfCancellationRequested();

                if (allDuctElements.Count > 0)
                {
                    ductData = ParameterProcessor.GetParameters(
                        allDuctElements,
                        _doc,
                        dbCache,
                        _sectionCache,
                        typeCaches,
                        wipCode);
                    LogMessage($"Обработано вентиляционных элементов: {ductData.Count}");
                }

                // ========== 6. КЭШ РОДИТЕЛЕЙ ==========
                var pipeParentData = new Dictionary<string, FamilyInfo>();
                var ductParentData = new Dictionary<string, FamilyInfo>();

                var pipeElementDocMap = new Dictionary<int, Document>();
                var ductElementDocMap = new Dictionary<int, Document>();

                foreach (var e in allPipeElements)
                {
                    if (!pipeElementDocMap.ContainsKey(e.Id.IntegerValue))
                    {
                        pipeElementDocMap[e.Id.IntegerValue] = e.Document;
                    }
                }

                foreach (var e in allDuctElements)
                {
                    if (!ductElementDocMap.ContainsKey(e.Id.IntegerValue))
                    {
                        ductElementDocMap[e.Id.IntegerValue] = e.Document;
                    }
                }

                foreach (var item in pipeData)
                {
                    if (pipeElementDocMap.TryGetValue(item.ID, out var doc))
                    {
                        string key = $"{doc.GetHashCode()}_{item.ID}";
                        if (!pipeParentData.ContainsKey(key))
                        {
                            pipeParentData[key] = item;
                        }
                    }
                }

                foreach (var item in ductData)
                {
                    if (ductElementDocMap.TryGetValue(item.ID, out var doc))
                    {
                        string key = $"{doc.GetHashCode()}_{item.ID}";
                        if (!ductParentData.ContainsKey(key))
                        {
                            ductParentData[key] = item;
                        }
                    }
                }

                // ========== 7. КОМПЛЕКТАЦИИ ==========
                var pipeEquipment = new List<FamilyInfo>();
                var ductEquipment = new List<FamilyInfo>();

                if (allPipeElements.Count > 0)
                {
                    UpdateProgress(65, "Получение комплектаций труб...", "Поиск оборудования для труб");
                    cancellationToken.ThrowIfCancellationRequested();
                    pipeEquipment = EquipmentDetector.Equip(allPipeElements, _doc, dbCache, _sectionCache, pipeParentData, wipCode);
                    LogMessage($"Комплектаций для труб: {pipeEquipment.Count}");
                }

                if (allDuctElements.Count > 0)
                {
                    UpdateProgress(72, "Получение комплектаций воздуховодов...", "Поиск оборудования для воздуховодов");
                    cancellationToken.ThrowIfCancellationRequested();
                    ductEquipment = EquipmentDetector.Equip(allDuctElements, _doc, dbCache, _sectionCache, ductParentData, wipCode);
                    LogMessage($"Комплектаций для воздуховодов: {ductEquipment.Count}");
                }

                // ========== 8. СОРТИРОВКА ==========
                if (pipeData.Count > 0)
                {
                    UpdateProgress(80, "Сортировка данных...", "Упорядочивание элементов");
                    cancellationToken.ThrowIfCancellationRequested();
                    pipeData = pipeData.OrderBy(x => x.SortingCode)
                                       .ThenBy(x => x.SystemName)
                                       .ThenBy(x => x.Order)
                                       .ToList();
                }

                // ========== 9. ЭКСПОРТ В MySQL ==========
                UpdateProgress(85, "Экспорт в MySQL...", "Подключение к базе данных");
                cancellationToken.ThrowIfCancellationRequested();

                string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson(DatabaseName);
                LogMessage($"Подключение к БД: {DatabaseName}");

                if (pipeData.Count > 0)
                {
                    LogMessage("Экспорт труб (OV1)...");
                    MySqlExporter.ExportToMySQL(pipeData, "OV1", connectionString);
                    LogMessage($"Трубы экспортированы: {pipeData.Count} записей");
                }

                if (ductData.Count > 0)
                {
                    LogMessage("Экспорт воздуховодов (OV2)...");
                    MySqlExporter.ExportToMySQL(ductData, "OV2", connectionString);
                    LogMessage($"Воздуховоды экспортированы: {ductData.Count} записей");
                }

                if (pipeEquipment.Count > 0)
                {
                    LogMessage("Экспорт комплектаций труб (OV1)...");
                    MySqlExporter.ExportToMySQL(pipeEquipment, "OV1", connectionString);
                    LogMessage($"Комплектации труб экспортированы: {pipeEquipment.Count} записей");
                }

                if (ductEquipment.Count > 0)
                {
                    LogMessage("Экспорт комплектаций воздуховодов (OV2)...");
                    MySqlExporter.ExportToMySQL(ductEquipment, "OV2", connectionString);
                    LogMessage($"Комплектации воздуховодов экспортированы: {ductEquipment.Count} записей");
                }

                stopwatch.Stop();

                string resultMessage = $"ИТОГО:\n" +
                                       $"Элементы трубопроводных систем: {pipeData.Count}\n" +
                                       $"Элементы воздуховодных систем: {ductData.Count}\n" +
                                       $"Комплектации труб: {pipeEquipment.Count}\n" +
                                       $"Комплектации воздуховодов: {ductEquipment.Count}\n" +
                                       $"Всего экспортировано: {pipeData.Count + ductData.Count + pipeEquipment.Count + ductEquipment.Count}\n" +
                                       $"Время экспорта: {stopwatch.Elapsed.TotalSeconds:F2} сек";

                UpdateProgress(100, "Готово!", resultMessage);
                LogMessage(resultMessage);
            }
            catch (OperationCanceledException)
            {
                LogMessage("Экспорт прерван пользователем");
                throw;
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка: {ex.Message}");
                throw;
            }
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

        private void UpdateProgress(int percent, string shortMessage, string detailMessage)
        {
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressValue = percent;
                    ProgressMaximum = 100;
                    ProgressText = $"{percent}%";
                    ProgressDetail = detailMessage;
                    StatusMessage = shortMessage;
                });
            }
            else
            {
                ProgressValue = percent;
                ProgressMaximum = 100;
                ProgressText = $"{percent}%";
                ProgressDetail = detailMessage;
                StatusMessage = shortMessage;
            }
        }

        private void LogMessage(string message)
        {
            string logEntry = $"{DateTime.Now:HH:mm:ss} - {message}\n";

            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogMessages += logEntry;
                    if (LogMessages.Length > 10000)
                    {
                        LogMessages = LogMessages.Substring(LogMessages.Length - 10000);
                    }
                });
            }
            else
            {
                LogMessages += logEntry;
                if (LogMessages.Length > 10000)
                {
                    LogMessages = LogMessages.Substring(LogMessages.Length - 10000);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ========== RELAY COMMAND ==========

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                if (_executeAsync != null)
                {
                    await _executeAsync();
                }
                else if (_execute != null)
                {
                    _execute();
                }
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}