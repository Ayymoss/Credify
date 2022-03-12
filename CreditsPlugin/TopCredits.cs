using System.Text.Json;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;

namespace CreditsPlugin;


public class TopCredits
{
    public static void LoadTopCredits(string topCredits)
    {
        //Convert string to JSON
        //Manipulate JSON into ordered list.
        
        
    }
    public static void CompareTop(EFClient client)
    {
        //Compare client to json data
        //If is higher than a client, replace the one below.
        //Write back to the database. 
    }
    
    //TODO: Implement this properly. I'm stuck. :(
    //public static void ReadJson()
    //{
    //    const string fileName = @"Configuration.json";
    //    var jsonString = File.ReadAllText(fileName);
    //    var jsonFormatted = JsonSerializer.Deserialize<JsonData>(jsonString)!;
    //    var jsonData = new JsonData
    //    {
    //        ApiKey = jsonFormatted.ApiKey
    //    };
    //}
//
    //public static void WriteJson()
    //{
    //    var jsonData = new JsonData
    //    {
    //        ApiKey = "ChangeMe"
    //    };
    //            
    //    var jsonString = JsonSerializer.Serialize(jsonData);
    //    File.WriteAllText("Configuration.json", jsonString);
    //}
}