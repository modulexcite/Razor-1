// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNet.Razor.Chunks.Generators;

namespace Microsoft.AspNet.Razor.Parser.SyntaxTree
{
    public class BlockBuilder
    {
        public BlockBuilder()
        {
            Reset();
        }

        public BlockBuilder(Block original)
        {
            Type = original.Type;
            Children = new List<SyntaxTreeNode>(original.Children);
            ChunkGenerator = original.ChunkGenerator;
        }

        [SuppressMessage(
            "Microsoft.Naming",
            "CA1721:PropertyNamesShouldNotMatchGetMethods",
            Justification = "Type is the most appropriate name for this property and there is little chance of " +
            "confusion with GetType")]
        public BlockType? Type { get; set; }
        public IList<SyntaxTreeNode> Children { get; private set; }
        public IParentChunkGenerator ChunkGenerator { get; set; }

        public virtual Block Build()
        {
            return new Block(this);
        }

        public virtual void Reset()
        {
            Type = null;
            Children = new List<SyntaxTreeNode>();
            ChunkGenerator = ParentChunkGenerator.Null;
        }
    }
}
