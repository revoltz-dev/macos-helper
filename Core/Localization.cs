namespace MacOSHelper.Core;

public enum Lang { Pt, En }

public static class T
{
    public static Lang Current { get; set; } = Lang.En;
    public static bool IsEn => Current == Lang.En;
    private static string P(string pt, string en) => IsEn ? en : pt;

    // Common
    public static string Cancel        => P("Cancelar",       "Cancel");
    public static string Refresh       => P("Atualizar",      "Refresh");
    public static string Loading       => P("Carregando...",  "Loading...");
    public static string Ready         => P("Pronto",         "Ready");
    public static string Working       => P("Trabalhando...", "Working...");
    public static string Copy          => P("Copiar",         "Copy");
    public static string LinesCount(int n) => P($"{n} linhas", $"{n} lines");

    // Footer
    public static string DevelopedBy   => P("desenvolvido por ", "developed by ");

    // Loading screen
    public static string Initializing  => P("Iniciando...",            "Starting...");
    public static string LoadingRes    => P("Carregando recursos...",  "Loading resources...");
    public static string ReadyDone     => P("Pronto.",                 "Ready.");
    public static string ErrorFmt(string m) => P($"Erro: {m}", $"Error: {m}");

    // Main header buttons
    public static string Catalog       => P("Catálogo",       "Catalog");
    public static string DetectMac     => P("Detectar Mac",   "Detect Mac");

    // UsbPage
    public static string UsbDriveLabel        => P("Pendrive:",        "USB Drive:");
    public static string UsbDrivePlaceholder  => P("(selecione um pendrive)",      "(select a USB drive)");
    public static string InstallerLabel       => P("Instalador:",      "Installer:");
    public static string InstallerPlaceholder => P("(nenhum instalador baixado)",  "(no installer downloaded)");
    public static string Search               => P("Buscar",           "Search");
    public static string PartitionsLabel(string s) => P($"    Partições: {s}", $"    Partitions: {s}");
    public static string NoPartitions         => P("nenhuma",          "none");
    public static string NoInstallerHint      => P("    Nenhum instalador baixado. Use Catálogo para baixar um.",
                                                    "    No installer downloaded. Use Catalog to download one.");
    public static string CreateBootableUsb    => P("Criar Pendrive Bootável", "Create Bootable USB");
    public static string ConfirmWipeTitle     => P("ATENÇÃO — ISSO VAI APAGAR TODOS OS DADOS DE:",
                                                    "WARNING — THIS WILL ERASE EVERYTHING ON:");
    public static string ConfirmContinue      => P("Continuar?",       "Continue?");
    public static string ConfirmYesErase      => P("Sim, apagar e criar", "Yes, erase and create");

    // CatalogPopup
    public static string CatalogTitle         => P("Catálogo macOS",  "macOS Catalog");
    public static string CatalogIntro         => P("Clique em 'Carregar' para buscar o catálogo da Apple.",
                                                    "Click 'Load' to fetch Apple's catalog.");
    public static string LoadCatalog          => P("Carregar Catálogo", "Load Catalog");
    public static string SeedRelease          => P("Release Público", "Public Release");
    public static string SeedPublicBeta       => P("Beta Público",    "Public Beta");
    public static string SeedCustomerSeed     => P("Customer Seed",   "Customer Seed");
    public static string SeedDeveloperBeta    => P("Developer Beta",  "Developer Beta");
    public static string FilteredFor(string id, string ver) => P($"Filtrado para {id}  (máx {ver})", $"Filtered for {id}  (max {ver})");
    public static string DetectFirstHint      => P("Dica: Detecte seu Mac primeiro para filtrar versões compatíveis.",
                                                    "Tip: detect your Mac first to filter compatible versions.");
    public static string ColVersion           => P("Versão",   "Version");
    public static string ColBuild             => P("Build",    "Build");
    public static string ColSize              => P("Tamanho",  "Size");
    public static string ColDate              => P("Data",     "Date");
    public static string ColAction            => P("Ação",     "Action");
    public static string Download             => P("Baixar",   "Download");
    public static string LoadedCount(int n)   => P($"Carregado: {n} versões encontradas.", $"Loaded: {n} versions found.");
    public static string FetchingMetaCount(int n) => P($"Encontrados {n} instaladores. Buscando metadados...",
                                                        $"Found {n} installers. Fetching metadata...");
    public static string DownloadingCatalog(string seed) => P($"Baixando catálogo ({seed})...", $"Downloading catalog ({seed})...");
    public static string ParsingCatalog       => P("Analisando catálogo...", "Parsing catalog...");

    // MacDetectPopup
    public static string DetectMacTitle       => P("Detectar Modelo do Mac", "Detect Mac Model");
    public static string DetectInstructions   => P("Execute um dos comandos abaixo no Terminal do seu Mac, depois cole o resultado:",
                                                    "Run one of the commands below in your Mac's Terminal, then paste the result:");
    public static string Or                   => P("  ou  ",  "  or  ");
    public static string PasteHere            => P("Cole o resultado aqui  (Ctrl+V):", "Paste the result here  (Ctrl+V):");
    public static string Detect               => P("Detectar", "Detect");
    public static string Identifier           => P("Identificador:", "Identifier:");
    public static string Model                => P("Modelo:",   "Model:");
    public static string MaxMacOS             => P("macOS máximo:", "Max macOS:");
    public static string ModelNotRecognized   => P("Modelo não reconhecido. Certifique-se de que o resultado contém algo como  hw.model: MacBookPro10,1",
                                                    "Model not recognized. Make sure the output contains something like  hw.model: MacBookPro10,1");

    // ProgressWidget
    public static string DownloadingLabel     => P("Baixando:", "Downloading:");
    public static string FileLabel(string n)  => P($"Arquivo: {n}", $"File: {n}");
    public static string SpeedLabel(double mb)=> P($"Velocidade: {mb:F2} MB/s", $"Speed: {mb:F2} MB/s");
    public static string TotalLabel           => P("Total:", "Total:");
    public static string StatusDownloading    => P("Baixando...", "Downloading...");
    public static string StatusCompleted      => P("Concluído",   "Completed");
    public static string StatusFailed         => P("Erro",         "Error");
    public static string StatusCancelled      => P("Cancelado",    "Cancelled");
    public static string StatusPaused         => P("Pausado",      "Paused");
    public static string StatusWaiting        => P("Aguardando",   "Waiting");
}
