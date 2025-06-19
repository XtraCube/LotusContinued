using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.Managers.Combo.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Lotus.Managers.Combo;

public class ComboListManager
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(ComboListManager));

    private static IDeserializer _comboListDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithDuplicateKeyChecking()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();
    private static ISerializer _comboListSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();

    private List<RoleComboInfo>? allCombos = [];

    private ComboListFile comboListFile;
    private FileInfo comboListFileInfo;

    internal ComboListManager(FileInfo comboListFileInfo)
    {
        this.comboListFileInfo = comboListFileInfo;
        ReloadComboList();
    }

    public List<RoleComboInfo> ListCombos => allCombos!.ToList();

    public byte CurrentPreset => comboListFile.CurrentPreset;

    internal void AddCombo(RoleComboInfo combo)
    {
        allCombos?.Add(combo);
        WriteComboList();
    }

    internal void RemoveCombo(RoleComboInfo combo)
    {
        if (allCombos?.Remove(combo) ?? false) WriteComboList();
    }

    public void ReloadComboList()
    {
        string result;
        using (StreamReader reader = new(comboListFileInfo.Open(FileMode.OpenOrCreate))) result = reader.ReadToEnd();
        comboListFile = result == null!
            ? new ComboListFile()
            : _comboListDeserializer.Deserialize<ComboListFile>(result);
        if (comboListFile == null!) comboListFile = new ComboListFile();
        allCombos = comboListFile.GetCurrentCombos();
    }

    public void ChangePreset(int change)
    {
        var newPreset = (int)comboListFile.CurrentPreset + change;
        if (newPreset < 1) newPreset = 5;
        else if (newPreset > 5) newPreset = 1;
        comboListFile.CurrentPreset = (byte)newPreset;
        allCombos = comboListFile.GetCurrentCombos();
    }

    public void WriteComboList()
    {
        string yaml = _comboListSerializer.Serialize(comboListFile);
        using FileStream stream = comboListFileInfo.Open(FileMode.Create);
        stream.Write(Encoding.UTF8.GetBytes(yaml));
    }
}