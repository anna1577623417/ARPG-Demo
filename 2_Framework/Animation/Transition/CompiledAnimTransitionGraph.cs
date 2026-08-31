using System;
using UnityEngine;

/// <summary>
/// 243.7 — Compiler output only. Runtime consumers read these flat arrays and never traverse
/// GraphProcessor nodes, graph views, AssetDatabase, or authoring edge metadata.
/// </summary>
public sealed class CompiledAnimTransitionGraph : ScriptableObject
{
    [SerializeField] int schemaVersion;
    [SerializeField] string graphGuid;
    [SerializeField] string graphHash;
    [SerializeField] CompiledAnimTransitionNode[] compiledNodes = Array.Empty<CompiledAnimTransitionNode>();
    [SerializeField] CompiledAnimTransitionLink[] compiledLinks = Array.Empty<CompiledAnimTransitionLink>();
    [SerializeField] string[] predicateTable = Array.Empty<string>();
    [SerializeField] AnimTransitionPolicyHandle[] policyHandles = Array.Empty<AnimTransitionPolicyHandle>();
    [SerializeField] CompiledAnimTransitionOutput[] outputTable = Array.Empty<CompiledAnimTransitionOutput>();
    [SerializeField] CompiledAnimationTransitionRule244[] ruleTable = Array.Empty<CompiledAnimationTransitionRule244>();
    [SerializeField] CompiledAnimationTransitionPolicy244[] typedPolicyTable = Array.Empty<CompiledAnimationTransitionPolicy244>();

    public int SchemaVersion => schemaVersion;
    public string GraphGuid => graphGuid ?? string.Empty;
    public string GraphHash => graphHash ?? string.Empty;
    public int NodeCount => compiledNodes?.Length ?? 0;
    public int LinkCount => compiledLinks?.Length ?? 0;
    public int OutputCount => outputTable?.Length ?? 0;
    public int RuleCount => ruleTable?.Length ?? 0;
    public int TypedPolicyCount => typedPolicyTable?.Length ?? 0;

    public void EditorInitialize(
        int version,
        string sourceGraphGuid,
        string hash,
        CompiledAnimTransitionNode[] nodes,
        CompiledAnimTransitionLink[] links,
        string[] predicates,
        AnimTransitionPolicyHandle[] policies,
        CompiledAnimTransitionOutput[] outputs)
    {
        schemaVersion = version;
        graphGuid = sourceGraphGuid ?? string.Empty;
        graphHash = hash ?? string.Empty;
        compiledNodes = nodes ?? Array.Empty<CompiledAnimTransitionNode>();
        compiledLinks = links ?? Array.Empty<CompiledAnimTransitionLink>();
        predicateTable = predicates ?? Array.Empty<string>();
        policyHandles = policies ?? Array.Empty<AnimTransitionPolicyHandle>();
        outputTable = outputs ?? Array.Empty<CompiledAnimTransitionOutput>();
    }

    public void EditorInitializeTypedTables(
        CompiledAnimationTransitionRule244[] rules,
        CompiledAnimationTransitionPolicy244[] typedPolicies)
    {
        ruleTable = rules ?? Array.Empty<CompiledAnimationTransitionRule244>();
        typedPolicyTable = typedPolicies ?? Array.Empty<CompiledAnimationTransitionPolicy244>();
    }

    public bool TryGetNode(int index, out CompiledAnimTransitionNode node)
    {
        if (compiledNodes != null && index >= 0 && index < compiledNodes.Length)
        {
            node = compiledNodes[index];
            return true;
        }

        node = default;
        return false;
    }

    public bool TryGetLink(int index, out CompiledAnimTransitionLink link)
    {
        if (compiledLinks != null && index >= 0 && index < compiledLinks.Length)
        {
            link = compiledLinks[index];
            return true;
        }

        link = default;
        return false;
    }

    public bool TryGetOutput(int index, out CompiledAnimTransitionOutput output)
    {
        if (outputTable != null && index >= 0 && index < outputTable.Length)
        {
            output = outputTable[index];
            return true;
        }

        output = default;
        return false;
    }

    public bool TryGetPolicy(int index, out AnimTransitionPolicyHandle policy)
    {
        if (policyHandles != null && index >= 0 && index < policyHandles.Length)
        {
            policy = policyHandles[index];
            return true;
        }

        policy = default;
        return false;
    }

    public bool TryGetRule(int index, out CompiledAnimationTransitionRule244 rule)
    {
        if (ruleTable != null && index >= 0 && index < ruleTable.Length)
        {
            rule = ruleTable[index];
            return true;
        }

        rule = default;
        return false;
    }

    public bool TryGetTypedPolicy(int index, out CompiledAnimationTransitionPolicy244 policy)
    {
        if (typedPolicyTable != null && index >= 0 && index < typedPolicyTable.Length)
        {
            policy = typedPolicyTable[index];
            return true;
        }

        policy = default;
        return false;
    }
}
