namespace Breezeblocks.HideoutSystem
{

public enum HideoutJobType
{
    Furto,
    Roubo,
    Latrocinio,
    Extorsao,
    Sequestro,
    Sabotagem,
    Assassinato,
    QueimaDeArquivos
}

public static class HideoutJobTypeUtility
{
    /// <summary>
    /// Returns the player-facing Portuguese name for a job type.
    /// </summary>
    public static string GetDisplayName(HideoutJobType jobType)
    {
        return jobType switch
        {
            HideoutJobType.Furto => "Furto",
            HideoutJobType.Roubo => "Roubo",
            HideoutJobType.Latrocinio => "Latrocínio",
            HideoutJobType.Extorsao => "Extorsão",
            HideoutJobType.Sequestro => "Sequestro",
            HideoutJobType.Sabotagem => "Sabotagem",
            HideoutJobType.Assassinato => "Assassinato",
            HideoutJobType.QueimaDeArquivos => "Queima de Arquivos",
            _ => "Furto"
        };
    }

    /// <summary>
    /// Returns the player-facing explanation for a job type.
    /// </summary>
    public static string GetDescription(HideoutJobType jobType)
    {
        return jobType switch
        {
            HideoutJobType.Furto => "Roube algo sem ser percebido.",
            HideoutJobType.Roubo => "Roube algo, sem restrições de visibilidade.",
            HideoutJobType.Latrocinio => "Roube algo a todo custo.",
            HideoutJobType.Extorsao => "Machuque alguém para obter informação ou por pura ameaça.",
            HideoutJobType.Sequestro => "Roube alguém de sua liberdade, viva.",
            HideoutJobType.Sabotagem => "Destrua bens materiais como forma de ameaça.",
            HideoutJobType.Assassinato => "Mate alguém.",
            HideoutJobType.QueimaDeArquivos => "Destrua arquivos importantes para prevenir consequências desastrosas.",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Returns the experience reward contributed by a job type.
    /// </summary>
    public static int GetExperienceReward(HideoutJobType jobType)
    {
        return jobType switch
        {
            HideoutJobType.Furto => 20,
            HideoutJobType.Roubo => 20,
            HideoutJobType.Latrocinio => 30,
            HideoutJobType.Extorsao => 35,
            HideoutJobType.Sequestro => 60,
            HideoutJobType.Sabotagem => 40,
            HideoutJobType.Assassinato => 85,
            HideoutJobType.QueimaDeArquivos => 100,
            _ => 0
        };
    }
}

}
