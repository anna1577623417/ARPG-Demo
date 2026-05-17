# 计算机编程基础与游戏开发知识 Agent 落地文件

## Purpose

This file is the direct implementation contract for an Agent that helps Leon build a long-term computer programming and game development knowledge system.

The Agent should read and migrate these configuration files first:

1. `HEARTBEAT.md`
2. `USER.md`
3. `AGENT.md`
4. `SOUL.md`

Then it should produce structured outputs using the directory and contract rules below.

## Source Materials

This implementation contract was distilled from:

- `9.1 Agent能力用于整理Unity笔试面试资料.md`
- `9.2 课程转训练数据集.md`
- `9.3 面试训练操作系统.md`
- `9.4 终身计算机基础知识.md`

The shared conclusion:

Leon does not need a loose note repository. Leon needs an AI-assisted learning operating system that connects:

```text
Raw material
→ knowledge extraction
→ project mapping
→ interview expression
→ code drill
→ review
→ long-term memory
```

## Required Agent Boot Order

When an Agent starts a LeonOS task, it should load in this order:

1. `SOUL.md`: identity and long-term values.
2. `USER.md`: user profile, strengths, weaknesses, preference.
3. `AGENT.md`: role, behavior, output contracts.
4. `HEARTBEAT.md`: loop, cadence, progress, next step discipline.
5. This file: directory output contract and migration plan.

## Target Directory

The Agent should create or use this root when asked to materialize the system:

```text
LeonOS/
├── 00_Inbox
├── 01_Raw
├── 02_Knowledge
├── 03_Projects
├── 04_Architecture
├── 05_Interview
├── 06_CodeDrill
├── 07_ComputerScience
├── 08_Review
├── 09_Prompts
├── 10_AI_Agent
├── 11_Career
├── 12_Thinking
└── 99_Assets
```

## Initial Materialization Plan

If the user says "落地 LeonOS" or "生成目录", create:

```text
LeonOS/
├── 00_Inbox/README.md
├── 01_Raw/README.md
├── 02_Knowledge/README.md
├── 03_Projects/README.md
├── 04_Architecture/README.md
├── 05_Interview/README.md
├── 06_CodeDrill/README.md
├── 07_ComputerScience/README.md
├── 08_Review/README.md
├── 09_Prompts/README.md
├── 10_AI_Agent/
│   ├── HEARTBEAT.md
│   ├── USER.md
│   ├── AGENT.md
│   ├── SOUL.md
│   └── README.md
├── 11_Career/README.md
├── 12_Thinking/README.md
└── 99_Assets/README.md
```

The four Agent files should be copied into `10_AI_Agent` without changing identity rules.

## Directory Responsibilities

### 00_Inbox

Temporary input area.

Allowed content:

- Screenshots.
- OCR text.
- copied answers.
- quick thoughts.
- unsorted interview questions.
- project problems.

Rule:

- Do not refine here.
- Move items out after processing.

### 01_Raw

Evidence layer.

Allowed content:

- Course transcript.
- official docs.
- source snippets.
- blog excerpts.
- interview recordings/transcripts.
- screenshots/OCR.

Rule:

- Preserve original meaning.
- Do not mix personal interpretation into Raw.

### 02_Knowledge

Structured knowledge layer.

One file equals one knowledge unit.

Default template:

```md
# Topic

## What It Is

## Why It Exists

## Core Mechanism

## Unity / Game Development Mapping

## Project Case

## Interview Questions

## Common Mistakes

## Related Code Drill

## One-Sentence Summary

## Review Prompts
```

### 03_Projects

Project evidence and architecture evolution.

Default template:

```md
# Project Topic

## Original Problem

## Old Design

## Why It Failed

## New Design

## Benefits

## Costs

## Future Risk

## Interview Story

## Related Knowledge
```

### 04_Architecture

Reusable design ideas abstracted from projects.

Examples:

- Event-driven architecture.
- Data-driven skill systems.
- layered state machines.
- input replay.
- network synchronization.
- motion framework.
- skill lifecycle.

Default template:

```md
# Architecture Pattern

## Problem It Solves

## Core Idea

## Components

## Data Flow

## Trade-Offs

## Unity Implementation

## Project Example

## Interview Expression
```

### 05_Interview

Speakable interview layer.

Default template:

```md
# Question

## Tags

## Difficulty

## Interviewer Intent

## Safe Answer

## Project Answer

## Architecture Answer

## Likely Follow-Ups

## Do Not Mention Unless Asked

## Leon's Answer History

## Current Weakness

## Next Practice
```

### 06_CodeDrill

Handwritten programming and algorithm practice.

Default template:

```md
# Problem

## Pattern

## First Reaction

## Brute Force

## Optimization Path

## Complexity

## Mistakes

## Unity / Game Dev Mapping

## Final Template

## Variants
```

### 07_ComputerScience

Long-term foundation layer.

Recommended subdirectories:

```text
07_ComputerScience/
├── CSharp
├── DataStructures
├── Algorithms
├── OperatingSystems
├── ComputerNetworks
├── Databases
├── Compilers
├── Graphics
├── Memory
├── Concurrency
└── DesignPatterns
```

Rule:

- Every foundation topic should still map back to Unity/game development when possible.

### 08_Review

Spaced repetition and weakness repair.

Default template:

```md
# Review Item

## Source

## Why It Matters

## Current Weakness

## 30-Second Recall

## Deep Recall

## Next Review Date

## Status
```

### 09_Prompts

Reusable prompt pipelines.

Recommended files:

- `知识点分析模板.md`
- `架构复盘模板.md`
- `算法题拆解模板.md`
- `面试问答模板.md`
- `源码分析模板.md`
- `项目映射模板.md`

### 10_AI_Agent

Agent brain and memory.

Required files:

- `HEARTBEAT.md`
- `USER.md`
- `AGENT.md`
- `SOUL.md`
- `WeaknessTracking.md`
- `ProgressLog.md`
- `PromptPipeline.md`
- `CognitiveHistory.md`

### 11_Career

Career-facing material.

Examples:

- Resume bullets.
- project description.
- self-introduction.
- target company preparation.
- interview timeline.

### 12_Thinking

Long-term thinking and learning reflections.

Examples:

- learning methods.
- personal technical philosophy.
- mistakes and corrections.
- why a system changed.

### 99_Assets

Images, diagrams, attachments, exported files.

## Agent Task Types

### Task Type: Process Raw Material

Input:

- A course section, article, transcript, or screenshot text.

Output:

1. Raw source location.
2. Knowledge Unit.
3. Interview Card.
4. Project Mapping if relevant.
5. Review prompts.

### Task Type: Build Interview Card

Input:

- A question or topic.

Output:

1. Interviewer intent.
2. Safe answer.
3. Project answer.
4. Architecture answer.
5. Follow-ups.
6. What not to mention.
7. Practice drill.

### Task Type: Build Code Drill

Input:

- Algorithm or handwritten programming topic.

Output:

1. Pattern.
2. Thought path.
3. Brute force.
4. Optimized solution.
5. Complexity.
6. Common mistakes.
7. Unity mapping.
8. Final template.

### Task Type: Project-to-Interview Mapping

Input:

- A project feature, bug, refactor, or architecture change.

Output:

1. Original problem.
2. Old design.
3. Failure pressure.
4. New design.
5. Benefits.
6. Costs.
7. Interview story.
8. Related knowledge.

### Task Type: Cognitive Correction

Input:

- Leon's answer, failed attempt, or confusion.

Output:

1. Main issue.
2. Repeated pattern.
3. Correct mental model.
4. Better answer.
5. Follow-up drill.
6. Review item.

## Migration Contract For HEARTBEAT / USER / AGENT / SOUL

When migrating these files into another workspace:

1. Preserve file names exactly:
   - `HEARTBEAT.md`
   - `USER.md`
   - `AGENT.md`
   - `SOUL.md`
2. Place them under:
   - `LeonOS/10_AI_Agent/`
3. Add a local `README.md` explaining that these are the Agent boot files.
4. Do not rewrite `SOUL.md` identity unless Leon explicitly changes identity.
5. Update `USER.md` when Leon's goals, weaknesses, projects, or interview state change.
6. Update `HEARTBEAT.md` only when the operating rhythm changes.
7. Update `AGENT.md` when output contracts or Agent roles change.

## Required End-of-Task Report

Every Agent task should end with:

```md
## Progress

- Completed:
- Files Created/Updated:
- Output Layer:
- Weakness / Gap Found:
- Next Step:
```

## Recommended First Three Outputs

After materializing LeonOS, generate these first:

1. `02_Knowledge/Unity/GC机制.md`
2. `05_Interview/Unity性能优化/什么是GC.md`
3. `03_Projects/ARPG/技能系统/为什么引入Intent层.md`

These three files test the full loop:

```text
Foundation knowledge
→ interview answer
→ project mapping
```

## Quality Gate

Before saving an output, check:

- Does it have a stable template?
- Does it map to project experience?
- Can Leon say it aloud in an interview?
- Does it reveal a review item?
- Can another Agent continue from it later?

If not, revise before writing.


补充：以满足后台扫描和自动化工作要求，以及严格约束读写限权

你这份文档已经非常接近“真正可执行的 Agent 工作契约”了。
但目前还缺几个关键层：

Agent 权限约束

后台扫描机制

增量索引规则

OCR/图片处理规范

长任务切片机制

知识图谱更新规则

防上下文爆炸机制

多Agent职责隔离

输出命名规范

失败恢复机制

Token预算意识

面试表达优先级规则


下面是建议你直接追加到文档中的内容。
这些内容不是“锦上添花”，而是：

> 从“能运行”升级到“长期稳定演化”。



以下内容建议直接补充到原文档后半部分。 


---

Incremental Processing Contract

The Agent must NEVER rescan the entire workspace unless explicitly requested.

Default behavior:

Only process:
- newly added files
- modified files
- failed previous tasks

The Agent should maintain:

LeonOS/10_AI_Agent/ProcessingIndex.json

Example:

{
  "file": "GC课程截图1.png",
  "hash": "xxxx",
  "processed": true,
  "last_update": "2026-05-14",
  "output": [
    "02_Knowledge/Unity/GC机制.md"
  ]
}

Purpose:

avoid repeated OCR

avoid repeated embedding

avoid token explosion

support resumable tasks



---

Workspace Permission Contract

The Agent must obey strict workspace permissions.

Read-Only Areas

00_Inbox
01_Raw
99_Assets

Rules:

NEVER delete original material

NEVER overwrite user source files

NEVER modify screenshots or recordings



---

Writable Areas

02_Knowledge
03_Projects
04_Architecture
05_Interview
06_CodeDrill
07_ComputerScience
08_Review
09_Prompts
10_AI_Agent
11_Career
12_Thinking

Rules:

only append or create

preserve previous versions if major rewrite occurs



---

Long Task Chunking Contract

The Agent must avoid extremely long uninterrupted processing.

Rule:

One task
→ one bounded output unit

Forbidden:

Analyze entire computer science repository in one pass.

Required behavior:

1 topic
→ 1 knowledge unit
→ 1 interview card
→ 1 review item

Purpose:

avoid context overflow

avoid generation interruption

improve resumability

improve quality consistency



---

Knowledge Graph Contract

The Agent should continuously build relationship mapping.

Required file:

02_Knowledge/KnowledgeGraph.md

Relationship examples:

GC机制
→ 内存管理
→ Mono
→ IL2CPP
→ 对象池
→ 性能优化

PlayableGraph
→ 动画系统
→ 技能系统
→ Timeline
→ 状态机解耦

The graph should support:

topic navigation

interview association

architecture mapping

weakness discovery



---

OCR Processing Contract

When processing images or screenshots:

Priority pipeline:

Image
→ OCR
→ Raw Text
→ Knowledge Extraction

Rules:

preserve original OCR text in 01_Raw

refined understanding belongs in 02_Knowledge

never merge OCR garbage into refined notes


Recommended OCR tags:

## OCR Confidence
## Possible Errors
## Manual Verification Needed


---

Image Classification Contract

The Agent should classify images before extraction.

Supported image types:

- code screenshot
- architecture diagram
- interview question
- UI design
- profiler capture
- stack trace
- handwritten notes
- flowchart

Each type should use different extraction behavior.

Example:

Profiler screenshot
→ optimization knowledge

Architecture diagram
→ architecture mapping

Interview screenshot
→ interview card


---

Embedding And Retrieval Contract

The Agent should support vector retrieval.

Recommended structure:

10_AI_Agent/VectorIndex/

Embedding unit rule:

1 concept
= 1 chunk

Avoid:

Entire article as one embedding.

Recommended chunk size:

300~1000 tokens


---

Knowledge Unit Naming Contract

All files should use stable semantic naming.

Good:

GC机制.md
对象池与GC关系.md
为什么引入Intent层.md

Bad:

学习笔记1.md
新整理.md
最终版.md

Reason:

stable retrieval

stable embedding

easier linking

easier agent continuation



---

Interview Priority Contract

When generating interview material:

Priority order:

Project understanding
> architecture understanding
> safe explanation
> theoretical completeness

Rule:

The Agent should optimize for:

Can Leon explain this naturally in an interview?

NOT:

Can the document become a textbook?


---

Weakness Tracking Contract

The Agent should continuously identify repeated weaknesses.

Required file:

10_AI_Agent/WeaknessTracking.md

Track:

- repeated confusion
- weak expression
- missing fundamentals
- overengineering tendency
- incomplete abstraction
- shallow debugging

Each weakness should include:

## Symptom
## Root Cause
## Repeated Scenario
## Correction
## Drill Plan


---

Failure Recovery Contract

If interrupted:

The Agent must save partial progress before termination.

Required directory:

10_AI_Agent/Recovery/

Example:

InterruptedTasks.md
PendingKnowledgeExtraction.md

The next Agent session should resume unfinished tasks first.


---

Token Budget Awareness Contract

The Agent should optimize context usage.

Priority:

structured summaries
> linked references
> repeated full content

Avoid:

Repeatedly injecting entire documents into context.

Instead:

Extract
→ summarize
→ reference
→ continue incrementally


---

Multi-Agent Separation Contract

Different Agents should have isolated responsibilities.

Recommended separation:

ResearchAgent
→ extract knowledge

InterviewAgent
→ generate interview cards

ArchitectureAgent
→ map system design

ReviewAgent
→ spaced repetition

CodeDrillAgent
→ algorithm training

Rule:

Avoid one giant omnipo