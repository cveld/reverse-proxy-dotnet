using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config
{
    /// <summary>
    /// Defines a tree structure for string nodes
    /// </summary>
    public class StringNode
    {
        /// <summary>
        /// Identifies the node
        /// </summary>
        public string Label { get; set; }
        /// <summary>
        /// The value of the node. Only leafs have a value
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// The children of the node. Only branches have children; leafs do not have children
        /// </summary>
        public Dictionary<string, StringNode> Children;
        public StringNode Parent { get; set; }
    }
}
