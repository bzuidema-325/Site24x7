using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    /// <summary>Parameter set names shared by the cmdlets.</summary>
    public static class ParameterSets
    {
        public const string List = "List";
        public const string ById = "ById";
        public const string ByName = "ByName";
    }

    /// <summary>Common shape for a Site24x7 collection such as /monitors or /tags.</summary>
    public abstract class ResourceCmdlet : Site24x7Cmdlet
    {
        /// <summary>The collection path relative to the API root, for example "monitor_groups".</summary>
        protected abstract string ResourcePath { get; }

        /// <summary>The id of the single resource the cmdlet targets, or null.</summary>
        protected virtual string ResourceId => null;

        /// <summary>The Accept header API version. Most areas use 2.0; monitor groups and threshold profiles use 2.1.</summary>
        protected virtual string ApiVersion => null;

        /// <summary>A readable name for confirmation prompts and messages, for example "monitor group".</summary>
        protected abstract string ResourceLabel { get; }

        protected string ItemPath => ResourcePath + "/" + Seg(ResourceId);

        /// <summary>"Site24x7.Monitor" for Get-Site24x7Monitor, and so on: the cmdlet noun is the type name.</summary>
        protected override string OutputTypeName
        {
            get
            {
                var noun = (MyInvocation.MyCommand as CmdletInfo)?.Noun;
                return noun != null && noun.StartsWith("Site24x7", StringComparison.Ordinal) && noun.Length > 8
                    ? "Site24x7." + noun.Substring(8)
                    : null;
            }
        }

        protected override object ErrorTarget => ResourceId;
    }

    /// <summary>
    /// Get-* cmdlets: with an id, GET {path}/{id}; without, GET {path} filtered by -Name.
    /// Derived classes declare the id parameter in the ById set and set DefaultParameterSetName = List.
    /// </summary>
    public abstract class GetResourceCmdlet : ResourceCmdlet
    {
        /// <summary>Filters the list by name. Wildcards are allowed.</summary>
        [Parameter(ParameterSetName = ParameterSets.List)]
        [SupportsWildcards]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>Extra query string values for the list request, such as paging or date filters.</summary>
        [Parameter(ParameterSetName = ParameterSets.List)]
        public Hashtable Query { get; set; }

        /// <summary>The property -Name is matched against. When an item lacks it, other common name fields are tried.</summary>
        protected virtual string NameProperty => "display_name";

        private static readonly string[] FallbackNameProperties =
            { "display_name", "profile_name", "name", "tag_name", "template_name", "schedule_name", "action_name", "label" };

        protected override void ProcessRecordCore()
        {
            if (!string.IsNullOrEmpty(ResourceId))
            {
                WriteData(Call("GET", ItemPath, apiVersion: ApiVersion));
                return;
            }

            WriteData(FilterByName(Call("GET", ResourcePath, query: Query, apiVersion: ApiVersion)));
        }

        protected object[] FilterByName(object data)
        {
            var items = Items(data);
            if (string.IsNullOrEmpty(Name)) return items.ToArray();
            var pattern = new WildcardPattern(Name, WildcardOptions.IgnoreCase);
            return items.Where(i => pattern.IsMatch(ItemName(i))).ToArray();
        }

        private string ItemName(object item)
        {
            var value = Json.Get(item, NameProperty)
                ?? FallbackNameProperties.Select(p => Json.Get(item, p)).FirstOrDefault(v => v != null);
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    /// <summary>Remove-* cmdlets: DELETE {path}/{id}, with confirmation. They write nothing on success.</summary>
    public abstract class RemoveResourceCmdlet : ResourceCmdlet
    {
        protected override void ProcessRecordCore()
        {
            var path = ItemPath;
            if (ShouldProcess(ResourceId, "Remove " + ResourceLabel))
            {
                Call("DELETE", path, apiVersion: ApiVersion);
            }
        }
    }

    /// <summary>
    /// New-* and Set-* cmdlets. Both build a JSON body from typed parameters plus -Property;
    /// New-* POSTs it to the collection and Set-* PUTs it to the item. Which one a cmdlet is
    /// follows from its verb, so derived classes only declare parameters.
    /// </summary>
    public abstract class WriteResourceCmdlet : ResourceCmdlet
    {
        /// <summary>
        /// Any other API fields, as a hashtable. Keys are the API's field names (for example
        /// @{ check_frequency = '5' }), and they override the typed parameters.
        /// </summary>
        [Parameter]
        public Hashtable Property { get; set; }

        /// <summary>True for Set-* cmdlets, false for New-*.</summary>
        protected bool IsUpdate => (MyInvocation.MyCommand as CmdletInfo)?.Verb == VerbsCommon.Set;

        /// <summary>Adds the typed parameters to the body.</summary>
        protected virtual void BuildBody(IDictionary body)
        {
        }

        /// <summary>Throws Site24x7ArgumentException when the body is not one the API can accept.</summary>
        protected virtual void ValidateBody(IDictionary body)
        {
            if (IsUpdate && body.Count == 0)
            {
                throw new Site24x7ArgumentException($"Nothing to update on the {ResourceLabel}. Pass at least one setting, or -Property.");
            }
        }

        /// <summary>Set-* cmdlets can replace the changes with a complete definition here, as Set-Site24x7Monitor -MergeWithCurrent does.</summary>
        protected virtual IDictionary PrepareUpdate(IDictionary changes) => changes;

        protected OrderedDictionary CreateBody()
        {
            var body = NewBody();
            BuildBody(body);
            Merge(body, Property);
            return body;
        }

        /// <summary>True when the caller set the field, with a typed parameter or in -Property.</summary>
        protected bool HasField(IDictionary body, string field) => body.Contains(field) || (Property != null && Property.ContainsKey(field));

        /// <summary>Sets a field to a default when the caller did not supply it in any form.</summary>
        protected void Default(IDictionary body, string field, object value)
        {
            if (!HasField(body, field)) body[field] = value;
        }

        // The body depends only on command-line parameters, so it is checked once, before any pipeline input.
        protected override void BeginProcessingCore() => ValidateBody(CreateBody());

        protected override void ProcessRecordCore()
        {
            if (IsUpdate)
            {
                var path = ItemPath;
                if (ShouldProcess(ResourceId, "Update " + ResourceLabel))
                {
                    WriteData(Call("PUT", path, PrepareUpdate(CreateBody()), apiVersion: ApiVersion));
                }
            }
            else
            {
                var body = CreateBody();
                if (ShouldProcess(Describe(body), "Create " + ResourceLabel))
                {
                    WriteData(Call("POST", ResourcePath, body, apiVersion: ApiVersion));
                }
            }
        }

        private string Describe(IDictionary body)
        {
            foreach (var key in new[] { "display_name", "profile_name", "name", "tag_name", "template_name", "credential_name", "action_name", "schedule_name", "label" })
            {
                if (body.Contains(key) && body[key] != null) return Convert.ToString(body[key], System.Globalization.CultureInfo.InvariantCulture);
            }
            return ResourceLabel;
        }
    }
}
