using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    /// <summary>Parameter set names shared by the resource cmdlets.</summary>
    public static class ParameterSets
    {
        public const string List = "List";
        public const string ById = "ById";
    }

    /// <summary>Common shape for a Site24x7 collection such as /monitors or /tags.</summary>
    public abstract class ResourceCmdlet : Site24x7Cmdlet
    {
        /// <summary>The collection path relative to the API root, for example "monitor_groups".</summary>
        protected abstract string ResourcePath { get; }

        /// <summary>The id of the single resource the cmdlet targets, or null.</summary>
        protected virtual string ResourceId => null;

        /// <summary>The Accept header API version. Most areas use 2.0; monitor groups use 2.1.</summary>
        protected virtual string ApiVersion => null;

        /// <summary>A readable name for confirmation prompts, for example "monitor group".</summary>
        protected virtual string ResourceLabel => GetType().Name;

        protected string ItemPath => ResourcePath + "/" + Seg(ResourceId);

        /// <summary>"Site24x7.Monitor" for Get-Site24x7Monitor, and so on.</summary>
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

            var data = Call("GET", ResourcePath, query: Query, apiVersion: ApiVersion);
            WriteData(FilterByName(data));
        }

        protected object FilterByName(object data)
        {
            if (string.IsNullOrEmpty(Name) || !(data is object[] items)) return data;
            var pattern = new WildcardPattern(Name, WildcardOptions.IgnoreCase);
            return items.Where(i => pattern.IsMatch(ItemName(i))).ToArray();
        }

        private string ItemName(object item)
        {
            var value = Json.Get(item, NameProperty);
            if (value == null)
            {
                value = FallbackNameProperties.Select(p => Json.Get(item, p)).FirstOrDefault(v => v != null);
            }
            return Convert.ToString(value) ?? string.Empty;
        }
    }

    /// <summary>Remove-* cmdlets: DELETE {path}/{id}, with confirmation.</summary>
    public abstract class RemoveResourceCmdlet : ResourceCmdlet
    {
        protected override void ProcessRecordCore()
        {
            if (ShouldProcess(ResourceId, "Remove " + ResourceLabel))
            {
                WriteData(Call("DELETE", ItemPath, apiVersion: ApiVersion));
            }
        }
    }

    /// <summary>New-* and Set-* cmdlets build a JSON body from typed parameters plus -Property.</summary>
    public abstract class WriteResourceCmdlet : ResourceCmdlet
    {
        /// <summary>
        /// Any other API fields, as a hashtable. Keys are the API's field names (for example
        /// @{ check_frequency = '5' }), and they override the typed parameters.
        /// </summary>
        [Parameter]
        [Alias("Body", "Properties")]
        public Hashtable Property { get; set; }

        /// <summary>Adds the typed parameters to the body.</summary>
        protected virtual void BuildBody(IDictionary body)
        {
        }

        protected OrderedDictionary CreateBody()
        {
            var body = NewBody();
            BuildBody(body);
            Merge(body, Property);
            return body;
        }

        protected static string Describe(IDictionary body, string fallback)
        {
            foreach (var key in new[] { "display_name", "profile_name", "name", "tag_name", "template_name", "label" })
            {
                if (body.Contains(key) && body[key] != null) return Convert.ToString(body[key]);
            }
            return fallback;
        }
    }

    /// <summary>POST {path}</summary>
    public abstract class NewResourceCmdlet : WriteResourceCmdlet
    {
        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create " + ResourceLabel))
            {
                WriteData(Call("POST", ResourcePath, body, apiVersion: ApiVersion));
            }
        }
    }

    /// <summary>PUT {path}/{id}. Only the fields you pass are sent.</summary>
    public abstract class SetResourceCmdlet : WriteResourceCmdlet
    {
        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0)
            {
                throw new Site24x7Exception($"Nothing to update on {ResourceLabel} {ResourceId}. Pass at least one setting or -Property.");
            }
            if (ShouldProcess(ResourceId, "Update " + ResourceLabel))
            {
                WriteData(Call("PUT", ItemPath, body, apiVersion: ApiVersion));
            }
        }
    }
}
