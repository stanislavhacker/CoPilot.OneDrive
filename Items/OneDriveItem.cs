using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoPilot.OneDrive.Items
{
    public class OneDriveItem
    {
        /// <summary>
        /// Id
        /// </summary>
        public string Id { get; private set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// Link
        /// </summary>
        public string Link { get; private set; }
        /// <summary>
        /// Source
        /// </summary>
        public string Source { get; private set; }
        /// <summary>
        /// Link
        /// </summary>
        public string Picture { get; private set; }
        /// <summary>
        /// ItemType
        /// </summary>
        public string ItemType { get; private set; }

        /// <summary>
        /// Is folder
        /// </summary>
        public bool IsFolder
        {
            get
            {
                if (!string.IsNullOrEmpty(this.ItemType))
                {
                    return this.ItemType.Equals("folder") || this.ItemType.Equals("album");
                }
                return false;
            }
        }

        /// <summary>
        /// FilesPath
        /// </summary>
        public string FilesPath
        {
            get
            {
                return IsFolder ? Id + "/files" : null;
            }
        }


        /// <summary>
        /// One drive item
        /// </summary>
        /// <param name="properties"></param>
        public OneDriveItem(IDictionary<string, object> properties)
        {
            if (properties.ContainsKey("id"))
            {
                this.Id = properties["id"] as string;
            }

            if (properties.ContainsKey("name"))
            {
                this.Name = properties["name"] as string;
            }

            if (properties.ContainsKey("type"))
            {
                this.ItemType = properties["type"] as string;
            }

            if (properties.ContainsKey("description"))
            {
                this.Description = properties["description"] as string;
            }

            if (properties.ContainsKey("picture"))
            {
                this.Picture = properties["picture"] as string;
            }

            if (properties.ContainsKey("source"))
            {
                this.Source = properties["source"] as string;
            }

            if (properties.ContainsKey("link"))
            {
                this.Link = properties["link"] as string;
            }
        }

        /// <summary>
        /// To string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.Name;
        }
    }
}
