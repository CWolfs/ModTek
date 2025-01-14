using BattleTech.Data;
using Newtonsoft.Json;

namespace ModTek.Features.CustomTags
{
    [JsonObject]
    internal class CustomTag
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public bool Important { get; set; }

        [JsonProperty]
        public bool PlayerVisible { get; set; }

        [JsonProperty]
        public string FriendlyName { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        public override string ToString()
        {
            return $"CustomTag => name: {Name}  important: {Important}  playerVisible: {PlayerVisible}  friendlyName: {FriendlyName}  description: {Description}";
        }

        public Tag_MDD ToTagMDD()
        {
            return new Tag_MDD(
                Name, Important, PlayerVisible,
                FriendlyName, Description
            );
        }
    }
}
