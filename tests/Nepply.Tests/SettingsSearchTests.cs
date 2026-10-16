using Nepply.Services;

internal static class SettingsSearchTests
{
    internal static void Run(Action<string,bool> check)
    {
        check("Settings search supports unaccented Vietnamese",SettingsSearch.Find("nhiet do").Any(e=>e.Control=="Sensors"));
        check("Settings search normalizes Vietnamese d stroke",SettingsSearch.Find("khoi dong").Any(e=>e.Control=="AutoStart"));
        check("Settings search handles mixed case and extra spaces",SettingsSearch.Find("  MAU   RIENG ").Single().Control=="ColorButton");
        check("Settings search routes grid editing to dot picker",SettingsSearch.Find("luoi doc").Single().Control=="GridPicker");
        check("Settings search has empty and no-result states",SettingsSearch.Find("  ").Length==0 && SettingsSearch.Find("xyz_no_match").Length==0);
        check("Settings search finds neon palette",SettingsSearch.Find("kim loai").Any(e=>e.Control=="AccentChoice"));
        check("Settings search removes obsolete compact island option",SettingsSearch.Find("island thu gon").Length==0);
        foreach(var (query,control,section) in new (string,string,int)[] {
            ("mau dong","DynamicMaterial",0), ("chieu cao canh tren","HeightSlider",1),
            ("vung cham mep","ZoneSlider",1), ("nhap dup","SingleClick",2),
            ("file an","HiddenFiles",2), ("control panel","SystemIcons",2),
            ("cham","GridPicker",2) })
        {
            var result=SettingsSearch.Find(query);
            check("Settings search routes missing option: "+control,result.Any(e=>e.Control==control && e.Section==section));
        }
        check("Search distinguishes hidden files from hiding Desktop icons",
            SettingsSearch.Find("file an").All(e=>e.Control!="HideIcons") && SettingsSearch.Find("an icon windows").Any(e=>e.Control=="HideIcons"));
    }
}
