# ClioAI Meeting Processing System - Detailed Requirements Document

**Version:** 1.0  
**Date:** December 2024  
**Project:** ClioAI Enhancement - Embeddings & Summarization

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Architecture](#2-system-architecture)
3. [Component Specifications](#3-component-specifications)
4. [Workflow Definitions](#4-workflow-definitions)
5. [Data Structures & Storage](#5-data-structures--storage)
6. [Processing Strategies](#6-processing-strategies)
7. [Integration Plans](#7-integration-plans)
8. [Configuration Management](#8-configuration-management)
9. [Testing & Validation](#9-testing--validation)
10. [Future Enhancements](#10-future-enhancements)

---

## 1. Executive Summary

### 1.1 Project Goals

Transform ClioAI from a real-time transcription tool into a comprehensive meeting intelligence system that:
- Captures and processes meeting audio in real-time
- Cleans transcripts using AI-powered methods
- Creates searchable embeddings for semantic retrieval
- Generates comprehensive summaries on-demand
- Enables conversational querying of past meetings (future)

### 1.2 Key Deliverables

1. **ClioEmbedder** - Standalone tool for transcript processing
2. **Enhanced ClioAI** - Integration of embedding pipeline
3. **Summarization Engine** - Agentic multi-stage summarizer
4. **ClioRAG** (Future) - Conversational meeting query interface

### 1.3 Constraints

- **VRAM Limit:** 8GB total
- **Context Window:** 32K tokens for summarization LLM
- **Processing Time:** < 15 minutes acceptable for 2-hour meeting
- **Quality Target:** 90%+ accuracy in cleanup and retrieval

---

## 2. System Architecture

### 2.1 Overall System Design

```
┌─────────────────────────────────────────────────────────────┐
│                         ClioAI                              │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Real-Time Transcription (Existing)                   │ │
│  │  - System audio capture (25-second chunks)            │ │
│  │  - Whisper server transcription                       │ │
│  │  - Live text buffer display                           │ │
│  │  - Meeting stop → Save raw_transcript.txt             │ │
│  └───────────────────────────────────────────────────────┘ │
│                           ↓                                 │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Post-Processing Pipeline (New)                       │ │
│  │  - Calls ClioEmbedder.exe                             │ │
│  │  - Shows progress bar                                 │ │
│  │  - Enables "Summarize" button when complete          │ │
│  └───────────────────────────────────────────────────────┘ │
│                           ↓                                 │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Summarization (New)                                  │ │
│  │  - Loads vector store                                 │ │
│  │  - Agentic retrieval & synthesis                      │ │
│  │  - Generates comprehensive summary                    │ │
│  │  - Displays in browser (MD → HTML)                    │ │
│  └───────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│              Persistent Meeting Storage                      │
│  meetings/                                                   │
│    2024-12-09-standup/                                      │
│      raw_transcript.txt                                     │
│      cleaned_transcript.txt                                 │
│      chunks.json                                            │
│      index.usearch                                          │
│      summary.md                                             │
│      summary.html                                           │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│               ClioRAG (Future Phase 2)                       │
│  - Load multiple meeting indexes                            │
│  - Conversational query interface                           │
│  - Cross-meeting search                                     │
│  - Timeline analysis                                        │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Component Relationships

```
┌──────────────┐
│   ClioAI     │ (Main application)
└──────┬───────┘
       │ calls
       ↓
┌──────────────┐
│ClioEmbedder  │ (Standalone CLI tool)
└──────┬───────┘
       │ uses
       ↓
┌──────────────────────────────────────────┐
│  Shared Infrastructure                   │
│  - LlamaSharp (LLM + embeddings)        │
│  - USearch (vector storage)             │
│  - Configuration files                   │
└──────────────────────────────────────────┘
       ↑
       │ used by
┌──────┴───────┐
│  ClioRAG     │ (Future - separate tool)
└──────────────┘
```

### 2.3 Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| **Language** | C# | .NET 6+ | All components |
| **LLM Framework** | LlamaSharp | Latest | Model loading & inference |
| **Embedding Model** | bge-small-en-v1.5 | Q8 | Semantic embeddings |
| **Vector Store** | USearch | Latest | Similarity search |
| **Cleanup Model** | TBD (Testing) | Q4 | Transcript cleanup |
| **Summary Model** | 7-8B Instruct | Q4 | Summary generation |
| **Transcription** | Whisper (existing) | - | Audio → Text |

---

## 3. Component Specifications

### 3.1 ClioEmbedder (New Standalone Tool)

#### 3.1.1 Purpose
Standalone CLI tool that transforms raw meeting transcripts into searchable embeddings.

#### 3.1.2 Inputs
- Raw transcript file (TXT)
- Configuration files (abbreviations.txt, backchannels.txt)
- Model paths (cleanup LLM, embedding model)

#### 3.1.3 Outputs
- Cleaned transcript with markers (TXT)
- Chunk definitions (JSON)
- Vector index (USearch binary)
- Processing metadata (JSON)
- Checkpoint files (for resumability)

#### 3.1.4 Command-Line Interface

```bash
# Basic usage
ClioEmbedder.exe --input "raw_transcript.txt" --output "meetings/2024-12-09"

# With options
ClioEmbedder.exe \
  --input "raw_transcript.txt" \
  --output "meetings/2024-12-09" \
  --cleanup-model "llama-3.1-8b-q4.gguf" \
  --embedding-model "bge-small-q8.gguf" \
  --config-dir "config/" \
  --detect-patterns  # Enable fine-tuning detection
  
# Resume from checkpoint
ClioEmbedder.exe --resume "meetings/2024-12-09"

# Reprocess with new config
ClioEmbedder.exe --reprocess "meetings/2024-12-09"
```

#### 3.1.5 Processing Stages

```
Stage 1: LLM Cleanup (8-10 minutes for 2-hour meeting)
├─ Load cleanup model (3B or 8B, TBD by testing)
├─ Split transcript into windows (2K tokens, 300 overlap)
├─ Process each window with LLM
├─ Save checkpoint after each window
├─ Merge windows (remove overlap)
├─ Output: cleaned_transcript_marked.txt
└─ Markers: [SENTENCE_END], [TOPIC_SHIFT: topic]

Stage 2: Parse & Chunk (< 1 second)
├─ Parse sentence markers
├─ Parse topic shift markers
├─ Group sentences into chunks (300-450 tokens)
├─ Respect topic boundaries when possible
├─ Output: chunks.json
└─ Metadata: chunk_id, text, topic, token_count

Stage 3: Generate Embeddings (3-5 minutes)
├─ Load bge-small embedding model
├─ For each chunk: generate 384-dim embedding
├─ Progress tracking
├─ Output: embeddings in memory
└─ Status updates every 10 chunks

Stage 4: Build Vector Store (< 1 second)
├─ Create USearch index (HNSW algorithm)
├─ Add all chunk embeddings
├─ Save to disk: index.usearch
├─ Save chunks.json
└─ Save metadata: meeting_info.json
```

#### 3.1.6 Error Handling & Recovery

```csharp
// Checkpoint structure
{
  "stage": "cleanup",
  "windows_completed": 15,
  "windows_total": 18,
  "last_checkpoint": "2024-12-09T15:30:45Z",
  "cleaned_segments": [...],
  "status": "in_progress"
}

// Recovery logic
if (checkpoint exists && status == "in_progress")
{
  resume from windows_completed
}
else if (checkpoint exists && status == "completed")
{
  skip to next stage
}
```

#### 3.1.7 Output File Structure

```
meetings/2024-12-09-standup/
├── raw_transcript.txt                    # Input (saved by ClioAI)
├── cleaned_transcript_marked.txt         # Stage 1 output
├── cleaned_transcript_marked.txt.checkpoint  # Stage 1 checkpoint
├── chunks.json                          # Stage 2 output
├── index.usearch                        # Stage 4 output
├── meeting_info.json                    # Metadata
└── processing_log.txt                   # Detailed log
```

**meeting_info.json structure:**
```json
{
  "meeting_id": "2024-12-09-standup",
  "date": "2024-12-09T10:00:00Z",
  "duration_minutes": 45,
  "total_chunks": 67,
  "embedding_model": "bge-small-en-v1.5-q8",
  "embedding_dimensions": 384,
  "cleanup_model": "llama-3.1-8b-q4",
  "processing_date": "2024-12-09T10:50:00Z",
  "processing_duration_seconds": 720,
  "transcript_stats": {
    "raw_chars": 45000,
    "cleaned_chars": 38000,
    "reduction_percent": 15.6
  }
}
```

---

### 3.2 ClioAI Integration (Updates to Existing)

#### 3.2.1 New UI Elements

**After meeting stops, add progress section:**
```
┌──────────────────────────────────────────────┐
│ ClioAI - Meeting Complete                   │
├──────────────────────────────────────────────┤
│                                              │
│ ✓ Transcript saved                          │
│                                              │
│ Processing for searchability...              │
│ [████████████████░░░░] 80%                  │
│ Status: Generating embeddings (60/75)       │
│                                              │
│ [Summarize] (disabled - processing...)      │
│                                              │
│ Estimated time remaining: 2 minutes          │
└──────────────────────────────────────────────┘
```

**After processing complete:**
```
┌──────────────────────────────────────────────┐
│ ClioAI - Meeting Complete                   │
├──────────────────────────────────────────────┤
│                                              │
│ ✓ Transcript saved                          │
│ ✓ Meeting processed and searchable          │
│                                              │
│ [Summarize Meeting]                         │
│                                              │
│ Meeting saved: meetings/2024-12-09-standup   │
└──────────────────────────────────────────────┘
```

#### 3.2.2 Integration Code Structure

```csharp
public class ClioAI
{
    // Existing real-time transcription code
    private AudioCapture audioCapture;
    private WhisperClient whisperClient;
    private StringBuilder transcriptBuffer;
    
    // New embedding integration
    private ClioEmbedderClient embedderClient;
    private string currentMeetingId;
    
    public async Task OnStopRecording()
    {
        // 1. Stop capture (existing)
        StopAudioCapture();
        
        // 2. Save transcript (existing)
        currentMeetingId = GenerateMeetingId(); // e.g., "2024-12-09-standup"
        var meetingDir = $"meetings/{currentMeetingId}";
        Directory.CreateDirectory(meetingDir);
        
        var transcriptPath = Path.Combine(meetingDir, "raw_transcript.txt");
        File.WriteAllText(transcriptPath, transcriptBuffer.ToString());
        
        UpdateUI("✓ Transcript saved");
        
        // 3. Start embedding process (new)
        UpdateUI("Processing for searchability...");
        ShowProgressBar(true);
        
        try
        {
            await embedderClient.ProcessMeetingAsync(
                transcriptPath,
                meetingDir,
                progressCallback: UpdateProgress
            );
            
            UpdateUI("✓ Meeting processed and searchable");
            EnableSummarizeButton(true);
        }
        catch (Exception ex)
        {
            UpdateUI($"⚠ Processing failed: {ex.Message}");
            LogError(ex);
            // Transcript still saved, can retry later
        }
        finally
        {
            ShowProgressBar(false);
        }
    }
    
    private void UpdateProgress(string status, int current, int total)
    {
        var percent = (int)((current / (double)total) * 100);
        UpdateProgressBar(percent);
        UpdateStatusText($"Status: {status} ({current}/{total})");
    }
    
    public async Task OnSummarizeClick()
    {
        UpdateUI("Generating summary...");
        ShowProgress("Analyzing meeting content...");
        
        try
        {
            var summary = await GenerateSummary(currentMeetingId);
            
            // Save MD
            var mdPath = Path.Combine($"meetings/{currentMeetingId}", "summary.md");
            File.WriteAllText(mdPath, summary);
            
            // Convert to HTML and display
            var html = ConvertToHTML(summary, currentMeetingId);
            var htmlPath = Path.Combine($"meetings/{currentMeetingId}", "summary.html");
            File.WriteAllText(htmlPath, html);
            
            // Open in browser
            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });
            
            UpdateUI("✓ Summary generated and opened");
        }
        catch (Exception ex)
        {
            UpdateUI($"⚠ Summary generation failed: {ex.Message}");
            LogError(ex);
        }
    }
}
```

#### 3.2.3 ClioEmbedder Client Wrapper

```csharp
public class ClioEmbedderClient
{
    public delegate void ProgressCallback(string status, int current, int total);
    
    public async Task ProcessMeetingAsync(
        string transcriptPath,
        string outputDir,
        ProgressCallback progressCallback)
    {
        // Option A: Call as subprocess (simpler, more robust)
        await CallAsSubprocess(transcriptPath, outputDir, progressCallback);
        
        // Option B: Direct library integration (future optimization)
        // await CallDirectly(transcriptPath, outputDir, progressCallback);
    }
    
    private async Task CallAsSubprocess(
        string transcriptPath,
        string outputDir,
        ProgressCallback progressCallback)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ClioEmbedder.exe",
            Arguments = $"--input \"{transcriptPath}\" --output \"{outputDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        
        // Parse output for progress
        while (!process.StandardOutput.EndOfStream)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            
            // Parse progress updates
            // Format: "PROGRESS|stage|current|total|status"
            if (line?.StartsWith("PROGRESS|") == true)
            {
                var parts = line.Split('|');
                var current = int.Parse(parts[2]);
                var total = int.Parse(parts[3]);
                var status = parts[4];
                
                progressCallback(status, current, total);
            }
        }
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"ClioEmbedder failed: {error}");
        }
    }
}
```

---

### 3.3 Summarization Engine (New Component)

#### 3.3.1 Architecture: Agentic Workflow

The summarization engine uses a multi-stage agentic approach that intelligently retrieves and synthesizes meeting content.

```
┌─────────────────────────────────────────────────────────┐
│              SUMMARIZATION AGENT WORKFLOW                │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Phase 1: DISCOVERY (Topic Identification)              │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Load all chunk embeddings                    │    │
│  │ • Cluster by semantic similarity (k-means)     │    │
│  │ • For each cluster: extract representative     │    │
│  │   chunks                                       │    │
│  │ • LLM labels each cluster → Topic list         │    │
│  │ Output: 5-7 main topics                        │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Phase 2: REFINEMENT (Topic Assignment)                 │
│  ┌────────────────────────────────────────────────┐    │
│  │ • For each discovered topic:                   │    │
│  │   - Query vector store: "discussion about X"   │    │
│  │   - Retrieve top 20 chunks                     │    │
│  │   - Merge with cluster assignments             │    │
│  │ Output: Comprehensive topic→chunks mapping     │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Phase 3: DETAIL EXTRACTION (Topic Sections)            │
│  ┌────────────────────────────────────────────────┐    │
│  │ • For each topic:                              │    │
│  │   - Get assigned chunks                        │    │
│  │   - Expand context (±2 chunks)                 │    │
│  │   - LLM: Generate detailed section             │    │
│  │ Output: Topic sections (3-5 paragraphs each)   │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Phase 4: SPECIAL QUERIES (Decisions & Actions)         │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Query: "What decisions were made?"           │    │
│  │   - Semantic + keyword search                  │    │
│  │   - Keywords: decided, agreed, will, conclusion│    │
│  │   - LLM: Extract decision list                 │    │
│  │                                                 │    │
│  │ • Query: "What action items?"                  │    │
│  │   - Keywords: action, task, follow up, will    │    │
│  │   - LLM: Extract actions with owners/dates     │    │
│  │                                                 │    │
│  │ Output: Decisions list, Actions list           │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Phase 5: SYNTHESIS (Final Summary)                     │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Combine all sections                         │    │
│  │ • Generate overview (2-3 paragraphs)           │    │
│  │ • Format as structured Markdown                │    │
│  │ • Convert to HTML with styling                 │    │
│  │ Output: Comprehensive summary document         │    │
│  └────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

#### 3.3.2 Implementation Structure

```csharp
public class SummarizationAgent
{
    private VectorStoreService vectorStore;
    private LLMService llm;
    private ClusteringService clustering;
    
    public async Task<string> GenerateSummary(string meetingDir)
    {
        var state = new AgentState();
        
        // Phase 1: Discovery
        state.Topics = await DiscoverTopics(meetingDir);
        
        // Phase 2: Refinement
        state.TopicChunks = await RefineTopicAssignments(state.Topics);
        
        // Phase 3: Detail Extraction
        state.TopicSections = await ExtractTopicDetails(state.TopicChunks);
        
        // Phase 4: Special Queries
        state.Decisions = await ExtractDecisions(meetingDir);
        state.Actions = await ExtractActionItems(meetingDir);
        
        // Phase 5: Synthesis
        var summary = await SynthesizeFinalSummary(state);
        
        return summary;
    }
    
    private async Task<List<Topic>> DiscoverTopics(string meetingDir)
    {
        // Load all embeddings
        var chunks = LoadChunks(meetingDir);
        var embeddings = chunks.Select(c => c.Embedding).ToArray();
        
        // Cluster (k-means, k=6)
        var clusters = clustering.KMeans(embeddings, k: 6);
        
        var topics = new List<Topic>();
        
        foreach (var cluster in clusters)
        {
            // Get representative chunks (closest to centroid)
            var clusterChunks = cluster.ChunkIndices
                .Select(i => chunks[i])
                .ToList();
            
            var representatives = GetRepresentativeChunks(clusterChunks, count: 5);
            
            // LLM labels the cluster
            var label = await LabelCluster(representatives);
            
            topics.Add(new Topic
            {
                Label = label,
                ChunkIndices = cluster.ChunkIndices,
                Representatives = representatives
            });
        }
        
        return topics;
    }
    
    private async Task<string> LabelCluster(List<Chunk> representatives)
    {
        var context = string.Join("\n\n", representatives.Select(c => c.Text));
        
        var prompt = $@"These excerpts discuss the same topic. What is the topic?
Return ONLY the topic name (2-5 words), no explanation.

Excerpts:
{context}

Topic:";
        
        var response = await llm.CompleteAsync(prompt, maxTokens: 20);
        return response.Trim();
    }
    
    private async Task<Dictionary<Topic, List<Chunk>>> RefineTopicAssignments(
        List<Topic> topics)
    {
        var refined = new Dictionary<Topic, List<Chunk>>();
        
        foreach (var topic in topics)
        {
            // Get chunks from clustering
            var baseChunks = topic.ChunkIndices
                .Select(i => allChunks[i])
                .ToList();
            
            // Query vector store for additional relevant chunks
            var query = $"detailed discussion about {topic.Label}";
            var queryEmbedding = embedder.GetEmbeddings(query);
            
            var retrieved = vectorStore.Search(queryEmbedding, topK: 30)
                .Where(r => !topic.ChunkIndices.Contains(r.ChunkIndex))
                .Where(r => r.Score > 0.6f)  // Threshold
                .Select(r => allChunks[r.ChunkIndex])
                .ToList();
            
            // Combine
            var allTopicChunks = baseChunks.Concat(retrieved).Distinct().ToList();
            
            refined[topic] = allTopicChunks;
        }
        
        return refined;
    }
    
    private async Task<Dictionary<Topic, string>> ExtractTopicDetails(
        Dictionary<Topic, List<Chunk>> topicChunks)
    {
        var sections = new Dictionary<Topic, string>();
        
        foreach (var (topic, chunks) in topicChunks)
        {
            // Expand context around chunks
            var expandedChunks = ExpandContext(chunks, windowSize: 2);
            
            // Build context for LLM
            var context = string.Join("\n\n", expandedChunks.Select(c => c.Text));
            
            var prompt = $@"Summarize the discussion about ""{topic.Label}"" from this meeting.

Include:
- Key points raised
- Different perspectives mentioned
- Conclusions or outcomes

Keep it concise but comprehensive (3-5 paragraphs).

Relevant excerpts:
{context}

Summary of {topic.Label}:";
            
            var section = await llm.CompleteAsync(prompt, maxTokens: 1000);
            sections[topic] = section;
        }
        
        return sections;
    }
    
    private async Task<List<string>> ExtractDecisions(string meetingDir)
    {
        var query = "decisions made, agreements reached, conclusions";
        var queryEmbedding = embedder.GetEmbeddings(query);
        
        // Hybrid: semantic + keyword
        var semanticResults = vectorStore.Search(queryEmbedding, topK: 30);
        
        var keywordBoosted = BoostByKeywords(
            semanticResults,
            keywords: new[] { "decided", "agreed", "will", "going to", "conclusion" }
        );
        
        var topChunks = keywordBoosted.Take(15).ToList();
        var context = string.Join("\n\n", topChunks.Select(c => c.Text));
        
        var prompt = $@"Extract specific decisions or agreements made in this meeting.
Format as a bullet list. Only include concrete decisions, not discussions.

Excerpts:
{context}

Decisions:";
        
        var response = await llm.CompleteAsync(prompt, maxTokens: 500);
        
        // Parse bullet list
        return response.Split('\n')
            .Where(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("•"))
            .Select(line => line.TrimStart('-', '•').Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();
    }
    
    private async Task<List<ActionItem>> ExtractActionItems(string meetingDir)
    {
        // Similar to ExtractDecisions but with action-specific keywords
        var query = "action items, tasks, assignments, next steps";
        var keywords = new[] { "will", "should", "need to", "action", "task", "follow up" };
        
        // ... similar retrieval logic ...
        
        var prompt = $@"Extract action items from this meeting.
For each action, try to identify: what, who (if mentioned), when (if mentioned)
Format as: - [Action] (Owner: [name], Due: [date])

Excerpts:
{context}

Action Items:";
        
        var response = await llm.CompleteAsync(prompt, maxTokens: 500);
        
        return ParseActionItems(response);
    }
    
    private async Task<string> SynthesizeFinalSummary(AgentState state)
    {
        var sb = new StringBuilder();
        
        // Generate overview
        var overview = await GenerateOverview(state);
        
        // Build final markdown
        sb.AppendLine("# Meeting Summary");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine(overview);
        sb.AppendLine();
        
        sb.AppendLine("## Topics Discussed");
        foreach (var (topic, section) in state.TopicSections)
        {
            sb.AppendLine($"### {topic.Label}");
            sb.AppendLine(section);
            sb.AppendLine();
        }
        
        sb.AppendLine("## Key Decisions");
        foreach (var decision in state.Decisions)
        {
            sb.AppendLine($"- {decision}");
        }
        sb.AppendLine();
        
        sb.AppendLine("## Action Items");
        foreach (var action in state.Actions)
        {
            sb.AppendLine($"- {action.Description}");
            if (!string.IsNullOrEmpty(action.Owner))
                sb.Append($" (Owner: {action.Owner})");
            if (action.DueDate.HasValue)
                sb.Append($" (Due: {action.DueDate:yyyy-MM-dd})");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}

public class AgentState
{
    public List<Topic> Topics { get; set; }
    public Dictionary<Topic, List<Chunk>> TopicChunks { get; set; }
    public Dictionary<Topic, string> TopicSections { get; set; }
    public List<string> Decisions { get; set; }
    public List<ActionItem> Actions { get; set; }
}
```

#### 3.3.3 Summary Output Format

**summary.md structure:**
```markdown
# Meeting Summary

## Overview
[2-3 paragraph high-level summary generated by LLM based on all topics]

## Topics Discussed

### Budget Planning for Q1
[3-5 paragraphs covering this topic's discussion]

### PLC Integration with SCADA
[3-5 paragraphs covering this topic's discussion]

### API Error Handling Strategy
[3-5 paragraphs covering this topic's discussion]

## Key Decisions
- Approved $50K budget for VFD upgrade in Q1
- Selected Allen-Bradley PLC for integration project
- Will implement retry logic with exponential backoff for API

## Action Items
- Review RTU configuration (Owner: Moses, Due: 2024-12-15)
- Update API error handling documentation (Owner: Engineering Team)
- Schedule follow-up meeting for integration planning (Due: 2024-12-20)

## Next Steps
[Brief paragraph on what happens next]
```

**summary.html enhancement:**
```html
<!DOCTYPE html>
<html>
<head>
    <title>Meeting Summary - 2024-12-09 Standup</title>
    <style>
        /* Professional styling */
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto;
            max-width: 900px;
            margin: 40px auto;
            padding: 20px;
        }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 30px;
        }
        .copy-btn {
            background: #007acc;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 5px;
            cursor: pointer;
        }
        /* Topic sections with collapsible */
        .topic { border-left: 4px solid #007acc; padding-left: 20px; }
        /* Decision and action items styling */
        .decision { background: #e8f5e9; padding: 10px; margin: 5px 0; }
        .action { background: #fff3e0; padding: 10px; margin: 5px 0; }
    </style>
</head>
<body>
    <div class="header">
        <h1>Meeting Summary</h1>
        <button class="copy-btn" onclick="copyToClipboard()">Copy to Clipboard</button>
    </div>
    
    <!-- Rendered Markdown content -->
    <div id="content">
        <!-- HTML from Markdown conversion -->
    </div>
    
    <script>
        function copyToClipboard() {
            const content = document.getElementById('content').innerText;
            navigator.clipboard.writeText(content).then(() => {
                const btn = document.querySelector('.copy-btn');
                btn.textContent = 'Copied!';
                setTimeout(() => btn.textContent = 'Copy to Clipboard', 2000);
            });
        }
    </script>
</body>
</html>
```

---

## 4. Workflow Definitions

### 4.1 End-to-End Meeting Processing Flow

```
┌─────────────────────────────────────────────────────────┐
│ PHASE 1: REAL-TIME CAPTURE (During Meeting)            │
├─────────────────────────────────────────────────────────┤
│ 1. User starts ClioAI                                   │
│ 2. Clicks "Start Recording"                             │
│ 3. System captures audio in 25-second chunks            │
│ 4. Each chunk sent to Whisper server                    │
│ 5. Transcribed text appended to buffer                  │
│ 6. User sees live transcript in text box                │
│ 7. Meeting ends, user clicks "Stop Recording"           │
│                                                          │
│ Duration: Real-time (meeting length)                    │
│ Output: raw_transcript.txt in buffer                    │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ PHASE 2: POST-PROCESSING (After Meeting)               │
├─────────────────────────────────────────────────────────┤
│ 1. ClioAI saves raw_transcript.txt to disk              │
│ 2. ClioAI generates meeting ID (e.g., 2024-12-09-15-30) │
│ 3. ClioAI creates meeting directory                     │
│ 4. ClioAI calls ClioEmbedder.exe as subprocess          │
│    ├─ Stage 1: LLM Cleanup (8-10 min)                  │
│    ├─ Stage 2: Parse & Chunk (1 sec)                   │
│    ├─ Stage 3: Generate Embeddings (3-5 min)           │
│    └─ Stage 4: Build Vector Store (1 sec)              │
│ 5. ClioAI shows progress bar during processing          │
│ 6. ClioEmbedder completes, returns success              │
│ 7. ClioAI enables "Summarize" button                    │
│                                                          │
│ Duration: 12-15 minutes for 2-hour meeting              │
│ Output: Searchable meeting in meetings/[id]/            │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ PHASE 3: SUMMARIZATION (On-Demand)                     │
├─────────────────────────────────────────────────────────┤
│ 1. User clicks "Summarize" button                       │
│ 2. ClioAI loads meeting directory                       │
│ 3. SummarizationAgent executes workflow:                │
│    ├─ Phase 1: Topic Discovery (30 sec)                │
│    ├─ Phase 2: Topic Refinement (15 sec)               │
│    ├─ Phase 3: Detail Extraction (1-2 min)             │
│    ├─ Phase 4: Special Queries (30 sec)                │
│    └─ Phase 5: Synthesis (30 sec)                      │
│ 4. Save summary.md                                      │
│ 5. Convert to summary.html                              │
│ 6. Open in browser                                      │
│                                                          │
│ Duration: 3-4 minutes                                   │
│ Output: Comprehensive summary document                  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ PHASE 4: QUERYING (Future - ClioRAG)                   │
├─────────────────────────────────────────────────────────┤
│ 1. User opens ClioRAG                                   │
│ 2. Sees list of all meetings                            │
│ 3. Types query: "What did we decide about budget?"      │
│ 4. ClioRAG searches relevant meeting(s)                 │
│ 5. Returns answer with sources                          │
│ 6. User can ask follow-up questions                     │
│                                                          │
│ Duration: < 5 seconds per query                         │
│ Output: Conversational answers with citations           │
└─────────────────────────────────────────────────────────┘
```

### 4.2 Cleanup Strategy Workflow (Testing Phase)

```
┌─────────────────────────────────────────────────────────┐
│ TESTING: Determine Best Cleanup Approach               │
├─────────────────────────────────────────────────────────┤
│                                                          │
│ Option A: 3B Model One-Shot (128K context)             │
│ ┌────────────────────────────────────────────────────┐ │
│ │ 1. Load llama-3.2-3B-Q4                            │ │
│ │ 2. Feed entire transcript (up to 30K tokens)       │ │
│ │ 3. Single LLM call with cleanup instructions       │ │
│ │ 4. Output: cleaned + chunked text with markers     │ │
│ │                                                     │ │
│ │ Pros: Simple, fast (~10 min)                       │ │
│ │ Cons: Lower quality (84% accuracy estimated)       │ │
│ │ VRAM: ~2.5GB                                       │ │
│ └────────────────────────────────────────────────────┘ │
│                                                          │
│ Option B: 8B Model Sliding Window (2K chunks)          │
│ ┌────────────────────────────────────────────────────┐ │
│ │ 1. Load llama-3.1-8B-Q4                            │ │
│ │ 2. Split transcript into 2K windows (300 overlap)  │ │
│ │ 3. Process each window (18 windows for 30K)        │ │
│ │ 4. Save checkpoint after each window               │ │
│ │ 5. Merge windows (remove overlap)                  │ │
│ │ 6. Output: cleaned text with markers               │ │
│ │                                                     │ │
│ │ Pros: Higher quality (93% accuracy estimated)      │ │
│ │ Cons: More complex, slower (~13 min)               │ │
│ │ VRAM: ~6GB                                         │ │
│ └────────────────────────────────────────────────────┘ │
│                                                          │
│ DECISION CRITERIA:                                      │
│ - Cleanup accuracy (backchannels removed correctly)    │
│ - Abbreviation preservation (technical terms intact)   │
│ - Semantic coherence (chunks make sense)               │
│ - Processing time (acceptable for user)                │
│ - VRAM usage (fits in 8GB budget)                      │
│                                                          │
│ TEST PLAN:                                              │
│ 1. Process 5 sample meetings with both options         │
│ 2. Manual quality review (checklist-based)             │
│ 3. Compare metrics (see section 9.2)                   │
│ 4. Choose winner or hybrid approach                    │
└─────────────────────────────────────────────────────────┘
```

### 4.3 Configuration Fine-Tuning Workflow

```
┌─────────────────────────────────────────────────────────┐
│ FINE-TUNING: Iterative Config Improvement              │
├─────────────────────────────────────────────────────────┤
│                                                          │
│ Week 1: Baseline                                        │
│ ├─ Start with default configs                           │
│ ├─ Process 10 test meetings                             │
│ └─ Establish baseline quality                           │
│                                                          │
│ Week 2-3: Detection & Learning                          │
│ ├─ Enable pattern detection                             │
│ ├─ Process 20-30 real meetings                          │
│ ├─ Review auto-detected patterns                        │
│ ├─ Identify missed abbreviations (VFD, RTU, etc.)      │
│ ├─ Identify missed backchannels (gotcha, for sure)     │
│ ├─ Update config files                                  │
│ └─ Reprocess meetings with updated configs              │
│                                                          │
│ Week 4+: Continuous Improvement                         │
│ ├─ Monitor new meetings for patterns                    │
│ ├─ Update configs as needed                             │
│ ├─ Quality improves over time                           │
│ └─ Configs become domain-specific knowledge base        │
│                                                          │
│ AUTO-DETECTION ALGORITHM:                               │
│ 1. Scan cleaned outputs for sentence fragments          │
│ 2. Identify patterns: "The X." where X is 2-5 letters   │
│ 3. Count frequency across meetings                      │
│ 4. If appears 5+ times across 3+ meetings → suggest     │
│ 5. Manual review and approval                           │
│ 6. Add to abbreviations.txt                             │
│ 7. Reprocess affected meetings                          │
└─────────────────────────────────────────────────────────┘
```

---

## 5. Data Structures & Storage

### 5.1 Chunk Data Structure

```csharp
public class Chunk
{
    // Identity
    public string Id { get; set; }              // "chunk-001"
    public int ChunkIndex { get; set; }         // 0-based index
    
    // Content
    public string Text { get; set; }            // Cleaned chunk text
    public int TokenCount { get; set; }         // Actual token count
    
    // Context
    public string Topic { get; set; }           // From LLM or clustering
    public List<int> SentenceIndices { get; set; }  // Which sentences included
    
    // Embedding
    public float[] Embedding { get; set; }      // 384-dim vector
    
    // Metadata
    public Dictionary<string, object> Metadata { get; set; }
}
```

**chunks.json format:**
```json
[
  {
    "id": "chunk-001",
    "chunk_index": 0,
    "text": "We discussed the VFD upgrade for the main production line. It needs to meet the latest NEC 2023 standards. The estimated budget is fifty thousand dollars for Q1.",
    "token_count": 387,
    "topic": "VFD Upgrade and Budget",
    "sentence_indices": [0, 1, 2, 3, 4],
    "metadata": {
      "start_position": 0,
      "end_position": 387,
      "has_technical_terms": true,
      "technical_terms": ["VFD", "NEC"]
    }
  },
  {
    "id": "chunk-002",
    "chunk_index": 1,
    "text": "The existing Allen-Bradley PLC can interface with the new SCADA system. However, we need to review the RTU configuration per the SOW requirements.",
    "token_count": 412,
    "topic": "PLC Integration",
    "sentence_indices": [5, 6, 7],
    "metadata": {
      "start_position": 388,
      "end_position": 800,
      "has_technical_terms": true,
      "technical_terms": ["PLC", "SCADA", "RTU", "SOW"]
    }
  }
]
```

### 5.2 Meeting Metadata Structure

```csharp
public class MeetingInfo
{
    public string MeetingId { get; set; }
    public DateTime Date { get; set; }
    public int DurationMinutes { get; set; }
    public List<string> Participants { get; set; }
    
    public TranscriptStats TranscriptStats { get; set; }
    public ProcessingInfo ProcessingInfo { get; set; }
    public ModelInfo ModelInfo { get; set; }
}

public class TranscriptStats
{
    public int RawCharacters { get; set; }
    public int CleanedCharacters { get; set; }
    public double ReductionPercent { get; set; }
    public int RawWords { get; set; }
    public int CleanedWords { get; set; }
    public int TotalChunks { get; set; }
}

public class ProcessingInfo
{
    public DateTime ProcessingDate { get; set; }
    public int ProcessingDurationSeconds { get; set; }
    public string ProcessingVersion { get; set; }
    public bool CompletedSuccessfully { get; set; }
    public List<string> Warnings { get; set; }
}

public class ModelInfo
{
    public string CleanupModel { get; set; }
    public string EmbeddingModel { get; set; }
    public int EmbeddingDimensions { get; set; }
    public int ContextWindow { get; set; }
}
```

### 5.3 Vector Store Schema

**USearch index properties:**
```
Index Type: HNSW (Hierarchical Navigable Small World)
Metric: Cosine Similarity
Dimensions: 384 (from bge-small)
Connectivity (M): 16 (edges per node in graph)
Expansion (ef_construction): 128 (search breadth during construction)

File format: Binary (.usearch)
Size estimate: ~150KB for 75 chunks (2KB per chunk)
```

**Retrieval parameters:**
```csharp
public class SearchParams
{
    public int TopK { get; set; } = 10;              // Return top 10 results
    public float MinScore { get; set; } = 0.5f;      // Minimum similarity threshold
    public int ExpandSearch { get; set; } = 40;      // ef_search parameter
}
```

### 5.4 Configuration File Formats

**config/abbreviations.txt:**
```
# Format: One abbreviation per line
# Lines starting with # are comments
# Case-insensitive matching

# Technical - Electrical Engineering
VFD
PLC
SCADA
NEC
RTU
HMI

# Technical - Software
API
SDK
REST
JSON

# Common
Dr.
Mr.
Mrs.
Inc.
etc.
```

**config/backchannels.txt:**
```
# Format: One phrase per line
# Used by both rule-based and LLM approaches

# Simple acknowledgments
yeah
yep
mm-hmm
uh-huh
okay
right
sure

# Understanding
i see
i understand
makes sense

# Fillers
um
uh
like
you know
```

---

## 6. Processing Strategies

### 6.1 Cleanup Strategy (Detailed)

#### 6.1.1 LLM Prompt Engineering

**Base prompt template:**
```
You are a meeting transcript cleanup assistant. Clean this segment by removing backchanneling and filler words while preserving ALL meaningful content.

REMOVE these backchannels:
{backchannels_list}

PRESERVE these technical abbreviations (do NOT break sentences at these):
{abbreviations_list}

RULES:
1. Remove standalone backchannels (yeah., mm-hmm., okay.)
2. Remove fillers at sentence start (um, uh, like)
3. Keep ALL technical terms from abbreviations list
4. Maintain sentence structure and meaning
5. Mark sentence boundaries with [SENTENCE_END]
6. Mark topic shifts with [TOPIC_SHIFT: brief topic description]

EXAMPLES:
Input: "So um we need to upgrade the VFD. Yeah. To meet NEC standards. Mm-hmm."
Output: "We need to upgrade the VFD to meet NEC standards. [SENTENCE_END]"

Input: "The PLC can interface with SCADA. I see. But we need to review the RTU. Okay."
Output: "The PLC can interface with SCADA. [SENTENCE_END] But we need to review the RTU. [SENTENCE_END]"

Raw segment:
{transcript_window}

Cleaned segment:
```

**Config injection:**
```csharp
private string BuildPrompt(string segment, List<string> backchannels, List<string> abbreviations)
{
    var backchannelList = string.Join(", ", backchannels.Take(20));
    var abbreviationList = string.Join(", ", abbreviations.Take(30));
    
    return template
        .Replace("{backchannels_list}", backchannelList)
        .Replace("{abbreviations_list}", abbreviationList)
        .Replace("{transcript_window}", segment);
}
```

#### 6.1.2 Sliding Window Parameters

**Window sizing:**
```
Window size: 2000 tokens
├─ Why: Fits comfortably in 4K context with room for prompt
├─ Leaves: ~2000 tokens for prompt + output
└─ Safe margin for model

Overlap: 300 tokens
├─ Why: Captures sentences that span window boundaries
├─ Percentage: 15% overlap
└─ Merged during post-processing

For 30,000 token meeting:
├─ Effective window: 2000 - 300 = 1700 tokens
├─ Number of windows: 30,000 / 1,700 ≈ 18 windows
└─ Processing time: 18 × 30-40 sec ≈ 9-12 minutes
```

**Overlap handling:**
```csharp
private string MergeWindows(List<CleanedSegment> segments)
{
    var merged = new StringBuilder();
    
    for (int i = 0; i < segments.Count; i++)
    {
        if (i == 0)
        {
            // First segment: include everything
            merged.Append(segments[i].CleanedText);
        }
        else
        {
            // Find overlap with previous segment
            var previousEnd = GetLastNTokens(segments[i-1].CleanedText, 300);
            var currentText = segments[i].CleanedText;
            
            // Find where previous ends in current
            var overlapIndex = FindOverlapStart(previousEnd, currentText);
            
            if (overlapIndex > 0)
            {
                // Skip the overlapping part
                merged.Append(currentText.Substring(overlapIndex));
            }
            else
            {
                // No clear overlap found, add space and append
                merged.Append(" " + currentText);
            }
        }
    }
    
    return merged.ToString();
}
```

#### 6.1.3 Checkpointing Strategy

**Checkpoint after each window:**
```csharp
public class CheckpointData
{
    public string MeetingId { get; set; }
    public string Stage { get; set; }  // "cleanup", "embedding", "vectorstore"
    public int WindowsCompleted { get; set; }
    public int WindowsTotal { get; set; }
    public DateTime LastCheckpoint { get; set; }
    public List<CleanedSegment> CleanedSegments { get; set; }
    public string Status { get; set; }  // "in_progress", "completed", "failed"
    public string ErrorMessage { get; set; }
}

// Save after each window
private void SaveCheckpoint(string outputPath, CheckpointData checkpoint)
{
    var checkpointPath = outputPath + ".checkpoint";
    var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
    
    // Atomic write: write to temp, then rename
    var tempPath = checkpointPath + ".tmp";
    File.WriteAllText(tempPath, json);
    File.Move(tempPath, checkpointPath, overwrite: true);
}

// Resume from checkpoint
private CheckpointData LoadCheckpoint(string outputPath)
{
    var checkpointPath = outputPath + ".checkpoint";
    
    if (!File.Exists(checkpointPath))
        return null;
    
    var json = File.ReadAllText(checkpointPath);
    return JsonSerializer.Deserialize<CheckpointData>(json);
}
```

**Resume logic:**
```csharp
public async Task<string> CleanTranscript(string rawTranscript, string outputPath)
{
    var checkpoint = LoadCheckpoint(outputPath);
    
    if (checkpoint != null && checkpoint.Status == "in_progress")
    {
        Console.WriteLine($"Resuming from window {checkpoint.WindowsCompleted}/{checkpoint.WindowsTotal}");
        
        var windows = CreateSlidingWindows(rawTranscript);
        var cleanedSegments = checkpoint.CleanedSegments;
        
        // Process remaining windows
        for (int i = checkpoint.WindowsCompleted; i < windows.Count; i++)
        {
            var cleaned = await CleanWindow(windows[i], i);
            cleanedSegments.Add(cleaned);
            
            checkpoint.WindowsCompleted = i + 1;
            checkpoint.LastCheckpoint = DateTime.Now;
            SaveCheckpoint(outputPath, checkpoint);
        }
        
        checkpoint.Status = "completed";
        SaveCheckpoint(outputPath, checkpoint);
        
        return MergeWindows(cleanedSegments);
    }
    
    // Start fresh...
}
```

### 6.2 Embedding Strategy

#### 6.2.1 Batch Processing

```csharp
public async Task<List<float[]>> GenerateEmbeddings(List<Chunk> chunks)
{
    var embeddings = new List<float[]>();
    
    // Load embedding model once
    var embedder = LoadEmbeddingModel("models/bge-small-q8.gguf");
    
    int batchSize = 10;  // Process in batches for better progress reporting
    
    for (int i = 0; i < chunks.Count; i += batchSize)
    {
        var batch = chunks.Skip(i).Take(batchSize).ToList();
        
        foreach (var chunk in batch)
        {
            var embedding = embedder.GetEmbeddings(chunk.Text);
            embeddings.Add(embedding);
            
            // Progress reporting
            ReportProgress("embedding", i + batch.IndexOf(chunk) + 1, chunks.Count);
        }
        
        // Small delay to prevent overheating (optional)
        await Task.Delay(100);
    }
    
    return embeddings;
}
```

#### 6.2.2 Embedding Quality Validation

```csharp
private void ValidateEmbedding(float[] embedding, string chunkText)
{
    // Check 1: Correct dimensions
    if (embedding.Length != 384)
    {
        throw new Exception($"Invalid embedding dimension: {embedding.Length}, expected 384");
    }
    
    // Check 2: Not all zeros (model failure)
    if (embedding.All(v => Math.Abs(v) < 0.0001f))
    {
        throw new Exception($"Zero embedding detected for chunk: {chunkText.Substring(0, 50)}...");
    }
    
    // Check 3: Magnitude in reasonable range
    var magnitude = Math.Sqrt(embedding.Sum(v => v * v));
    if (magnitude < 0.1 || magnitude > 10.0)
    {
        Console.WriteLine($"Warning: Unusual embedding magnitude {magnitude} for chunk");
    }
}
```

### 6.3 Retrieval Strategy

#### 6.3.1 Context Expansion

```csharp
private List<Chunk> ExpandContext(List<Chunk> retrievedChunks, int windowSize = 2)
{
    var expanded = new HashSet<Chunk>();
    
    foreach (var chunk in retrievedChunks)
    {
        // Add the chunk itself
        expanded.Add(chunk);
        
        // Add surrounding chunks
        for (int offset = -windowSize; offset <= windowSize; offset++)
        {
            if (offset == 0) continue;
            
            var adjacentIndex = chunk.ChunkIndex + offset;
            
            if (adjacentIndex >= 0 && adjacentIndex < totalChunks)
            {
                var adjacentChunk = allChunks[adjacentIndex];
                expanded.Add(adjacentChunk);
            }
        }
    }
    
    // Return in original order
    return expanded.OrderBy(c => c.ChunkIndex).ToList();
}
```

#### 6.3.2 Hybrid Retrieval (Semantic + Keyword)

```csharp
private List<Chunk> HybridSearch(
    string query, 
    float[] queryEmbedding,
    List<string> keywords,
    int topK = 10)
{
    // Semantic search
    var semanticResults = vectorStore.Search(queryEmbedding, topK: topK * 2);
    
    // Keyword boosting
    var scored = semanticResults.Select(result => 
    {
        var chunk = allChunks[result.ChunkIndex];
        
        // Calculate keyword score
        var keywordScore = CalculateKeywordScore(chunk.Text, keywords);
        
        // Combine scores (70% semantic, 30% keyword)
        var finalScore = 0.7f * result.Score + 0.3f * keywordScore;
        
        return (chunk, finalScore);
    });
    
    // Re-rank and return top K
    return scored
        .OrderByDescending(x => x.finalScore)
        .Take(topK)
        .Select(x => x.chunk)
        .ToList();
}

private float CalculateKeywordScore(string text, List<string> keywords)
{
    var lowerText = text.ToLower();
    var matchCount = keywords.Count(kw => lowerText.Contains(kw.ToLower()));
    return (float)matchCount / keywords.Count;
}
```

#### 6.3.3 Topic-Based Clustering

```csharp
public List<TopicCluster> ClusterByTopic(List<Chunk> chunks, int numClusters = 6)
{
    // Extract embeddings
    var embeddings = chunks.Select(c => c.Embedding).ToArray();
    
    // K-means clustering
    var assignments = KMeans(embeddings, k: numClusters);
    
    // Group chunks by cluster
    var clusters = new List<TopicCluster>();
    
    for (int i = 0; i < numClusters; i++)
    {
        var clusterChunks = chunks
            .Where((c, idx) => assignments[idx] == i)
            .ToList();
        
        if (clusterChunks.Any())
        {
            clusters.Add(new TopicCluster
            {
                ClusterId = i,
                Chunks = clusterChunks,
                Centroid = CalculateCentroid(clusterChunks.Select(c => c.Embedding))
            });
        }
    }
    
    return clusters;
}

private float[] CalculateCentroid(IEnumerable<float[]> embeddings)
{
    var embeddingsList = embeddings.ToList();
    var dimensions = embeddingsList[0].Length;
    var centroid = new float[dimensions];
    
    for (int i = 0; i < dimensions; i++)
    {
        centroid[i] = embeddingsList.Average(e => e[i]);
    }
    
    // Normalize
    var magnitude = Math.Sqrt(centroid.Sum(v => v * v));
    for (int i = 0; i < dimensions; i++)
    {
        centroid[i] /= (float)magnitude;
    }
    
    return centroid;
}
```

---

## 7. Integration Plans

### 7.1 ClioAI Update Plan

#### 7.1.1 Phase 1: Foundation (Week 1-2)

**Tasks:**
1. Build ClioEmbedder as standalone tool
2. Test with sample transcripts
3. Validate output quality
4. Establish baseline performance

**Deliverables:**
- Working ClioEmbedder.exe
- Test results document
- Performance benchmarks

#### 7.1.2 Phase 2: Integration (Week 3)

**Tasks:**
1. Add ClioEmbedderClient wrapper to ClioAI
2. Implement subprocess calling
3. Add progress bar UI
4. Implement "Summarize" button
5. Test end-to-end workflow

**Code changes in ClioAI:**
```csharp
// New files to add:
/Services/ClioEmbedderClient.cs
/Services/SummarizationService.cs
/Models/MeetingInfo.cs
/Models/ProcessingProgress.cs

// Modified files:
/MainWindow.xaml.cs (or equivalent UI code)
  - Add progress bar control
  - Add "Summarize" button
  - Wire up OnStopRecording event
  - Wire up OnSummarizeClick event
  
/Services/TranscriptionService.cs
  - Add call to ClioEmbedderClient after save
```

**UI mockup changes:**
```xml
<!-- Add to MainWindow.xaml -->
<StackPanel x:Name="ProcessingPanel" Visibility="Collapsed">
    <TextBlock Text="Processing for searchability..." FontWeight="Bold"/>
    <ProgressBar x:Name="ProcessingProgressBar" Height="20" Margin="0,10"/>
    <TextBlock x:Name="ProcessingStatus" Text="Status: Starting..."/>
    <TextBlock x:Name="ProcessingTime" Text="Est. time: calculating..."/>
</StackPanel>

<Button x:Name="SummarizeButton" 
        Content="Summarize Meeting" 
        IsEnabled="False"
        Click="OnSummarizeClick"
        Margin="0,20"/>
```

#### 7.1.3 Phase 3: Summarization (Week 4)

**Tasks:**
1. Implement SummarizationAgent
2. Implement clustering logic
3. Implement retrieval strategies
4. Implement MD → HTML conversion
5. Test summarization quality

**Integration:**
```csharp
// Add to ClioAI
private async void OnSummarizeClick(object sender, RoutedEventArgs e)
{
    try
    {
        SummarizeButton.IsEnabled = false;
        ShowProgress("Generating summary...");
        
        var summaryService = new SummarizationService();
        var meetingDir = $"meetings/{currentMeetingId}";
        
        UpdateProgress("Analyzing meeting structure...", 0, 5);
        var summary = await summaryService.GenerateSummary(meetingDir, progress =>
        {
            UpdateProgress(progress.Status, progress.Current, progress.Total);
        });
        
        UpdateProgress("Saving summary...", 5, 5);
        
        // Save and display
        var htmlPath = Path.Combine(meetingDir, "summary.html");
        File.WriteAllText(htmlPath, summary);
        
        // Open in browser
        Process.Start(new ProcessStartInfo
        {
            FileName = htmlPath,
            UseShellExecute = true
        });
        
        HideProgress();
        ShowNotification("Summary generated successfully!");
    }
    catch (Exception ex)
    {
        ShowError($"Failed to generate summary: {ex.Message}");
        LogError(ex);
    }
    finally
    {
        SummarizeButton.IsEnabled = true;
    }
}
```

#### 7.1.4 Phase 4: Polish & Testing (Week 5)

**Tasks:**
1. Error handling improvements
2. User feedback mechanisms
3. Performance optimization
4. Integration testing
5. User acceptance testing

**Quality gates:**
- [ ] All meetings process without errors
- [ ] Progress reporting is accurate
- [ ] Summaries are high quality (manual review)
- [ ] Processing time < 15 min for 2-hour meeting
- [ ] UI remains responsive during processing
- [ ] Error messages are clear and actionable

### 7.2 Deployment Strategy

#### 7.2.1 File Structure

```
ClioAI/
├── ClioAI.exe                          # Main application
├── ClioEmbedder.exe                    # Processing tool
├── config/
│   ├── abbreviations.txt
│   └── backchannels.txt
├── models/
│   ├── bge-small-en-v1.5-q8.gguf      # Embedding model
│   ├── llama-3.1-8b-q4.gguf           # Cleanup model (TBD by testing)
│   └── llama-3.1-8b-q4.gguf           # Summarization model
├── meetings/                           # Created on first run
│   └── [meeting directories]
└── logs/
    └── [log files]
```

#### 7.2.2 Installation Steps

1. Extract ClioAI package to desired location
2. Download models to models/ folder
3. First run: Creates config files with defaults
4. User can customize config files
5. Ready to use

#### 7.2.3 Upgrade Path

**From current ClioAI to enhanced version:**

```
1. Backup existing ClioAI folder
2. Install new version to separate folder
3. Copy existing meetings/ if any (for future ClioRAG)
4. Test with new meeting
5. If successful, replace old version
```

**Model updates:**
```
1. Download new model GGUF file
2. Place in models/ folder
3. Update config to point to new model
4. No code changes needed
```

---

## 8. Configuration Management

### 8.1 Configuration File Locations

```
config/
├── abbreviations.txt              # User-editable abbreviation list
├── backchannels.txt              # User-editable backchannel list
├── embedder_config.json          # ClioEmbedder settings
└── summarizer_config.json        # Summarization settings
```

### 8.2 Embedder Configuration

**embedder_config.json:**
```json
{
  "models": {
    "cleanup_model": "models/llama-3.1-8b-q4.gguf",
    "embedding_model": "models/bge-small-en-v1.5-q8.gguf",
    "context_size": 4096,
    "gpu_layers": 20
  },
  "cleanup": {
    "window_size": 2000,
    "overlap_size": 300,
    "temperature": 0.1,
    "max_tokens": 2048
  },
  "chunking": {
    "target_tokens": 400,
    "max_tokens": 500,
    "respect_topic_boundaries": true
  },
  "embedding": {
    "batch_size": 10,
    "validate_embeddings": true
  },
  "detection": {
    "enabled": true,
    "min_frequency": 5,
    "min_meetings": 3
  }
}
```

### 8.3 Summarizer Configuration

**summarizer_config.json:**
```json
{
  "model": {
    "path": "models/llama-3.1-8b-q4.gguf",
    "context_size": 32768,
    "gpu_layers": 35,
    "temperature": 0.3,
    "top_p": 0.9
  },
  "workflow": {
    "enable_topic_discovery": true,
    "enable_topic_refinement": true,
    "enable_detail_extraction": true,
    "enable_decision_extraction": true,
    "enable_action_extraction": true
  },
  "clustering": {
    "num_clusters": 6,
    "min_cluster_size": 3
  },
  "retrieval": {
    "top_k": 20,
    "min_score": 0.6,
    "context_window": 2,
    "hybrid_search": true,
    "keyword_weight": 0.3
  },
  "output": {
    "format": "markdown",
    "include_metadata": true,
    "generate_html": true,
    "html_template": "templates/summary_template.html"
  }
}
```

### 8.4 Dynamic Config Loading

```csharp
public class ConfigManager
{
    private static ConfigManager instance;
    private EmbedderConfig embedderConfig;
    private SummarizerConfig summarizerConfig;
    
    public static ConfigManager Instance => instance ??= new ConfigManager();
    
    private ConfigManager()
    {
        LoadConfigs();
    }
    
    public void LoadConfigs()
    {
        embedderConfig = LoadJsonConfig<EmbedderConfig>("config/embedder_config.json");
        summarizerConfig = LoadJsonConfig<SummarizerConfig>("config/summarizer_config.json");
    }
    
    public void ReloadConfigs()
    {
        LoadConfigs();
        Console.WriteLine("Configurations reloaded");
    }
    
    public EmbedderConfig Embedder => embedderConfig;
    public SummarizerConfig Summarizer => summarizerConfig;
    
    private T LoadJsonConfig<T>(string path) where T : new()
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new T();
            SaveJsonConfig(path, defaultConfig);
            return defaultConfig;
        }
        
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json);
    }
    
    private void SaveJsonConfig<T>(string path, T config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(path, json);
    }
}
```

---

## 9. Testing & Validation

### 9.1 Test Data Requirements

**Sample meetings needed:**
```
1. Short meeting (30 min)
   - Simple standup
   - 5-7 topics
   - Minimal backchanneling

2. Medium meeting (1 hour)
   - Technical discussion
   - 8-12 topics
   - Moderate backchanneling
   - Technical abbreviations

3. Long meeting (2 hours)
   - Planning session
   - 15-20 topics
   - Heavy backchanneling
   - Multiple speakers
   - Topic shifts

4. Complex meeting (1.5 hours)
   - Debate/discussion
   - Cross-talk
   - Multiple abbreviations
   - Decision points
   - Action items

5. Technical deep-dive (1 hour)
   - Heavy jargon
   - Technical abbreviations
   - Detailed discussions
```

### 9.2 Quality Metrics

#### 9.2.1 Cleanup Quality Metrics

```
Metric 1: Backchannel Removal Accuracy
├─ Method: Manual review of 100 random sentences
├─ Count: Backchannels correctly removed vs total
├─ Target: > 90%
└─ Formula: (Correctly removed / Total backchannels) × 100

Metric 2: Abbreviation Preservation
├─ Method: Check all known abbreviations in output
├─ Count: Abbreviations preserved vs split incorrectly
├─ Target: > 95%
└─ Formula: (Preserved / Total abbreviations) × 100

Metric 3: False Positive Rate
├─ Method: Count valid words incorrectly removed
├─ Examples: "right" (direction), "okay" (status)
├─ Target: < 5%
└─ Formula: (False positives / Total words removed) × 100

Metric 4: Semantic Coherence
├─ Method: Manual review of 50 random chunks
├─ Rating: 1-5 scale (does chunk make sense?)
├─ Target: Average > 4.0
└─ Formula: Sum of ratings / Number of chunks
```

#### 9.2.2 Retrieval Quality Metrics

```
Metric 1: Retrieval Precision
├─ Method: For 20 test queries, check if top 5 results are relevant
├─ Target: > 80% relevant
└─ Formula: (Relevant in top 5 / 5) × 100

Metric 2: Topic Coverage
├─ Method: For each major topic, can we retrieve relevant chunks?
├─ Target: All major topics retrievable
└─ Formula: (Topics retrieved / Total topics) × 100

Metric 3: Context Adequacy
├─ Method: Does retrieved context answer the query?
├─ Rating: 1-5 scale
├─ Target: Average > 4.0
└─ Manual evaluation
```

#### 9.2.3 Summary Quality Metrics

```
Metric 1: Topic Coverage
├─ Method: Compare summary topics vs actual meeting topics
├─ Target: 90%+ of major topics mentioned
└─ Formula: (Topics in summary / Actual topics) × 100

Metric 2: Decision Accuracy
├─ Method: Check if all decisions mentioned in meeting are in summary
├─ Target: 100% of decisions captured
└─ Manual comparison with meeting notes

Metric 3: Action Item Completeness
├─ Method: Compare action items in summary vs meeting
├─ Target: 100% of action items captured
└─ Manual verification

Metric 4: Hallucination Check
├─ Method: Verify all facts in summary appear in transcript
├─ Target: 0 hallucinations
└─ Manual fact-checking

Metric 5: Readability
├─ Method: Manual review by stakeholders
├─ Rating: 1-5 scale
├─ Target: Average > 4.0
└─ Subjective evaluation
```

### 9.3 Performance Benchmarks

```
Target benchmarks (2-hour meeting, 30K tokens):

Stage 1: Cleanup
├─ 3B model one-shot: < 10 minutes
├─ 8B model sliding window: < 13 minutes
└─ Target: < 15 minutes

Stage 2: Chunking
└─ Target: < 5 seconds

Stage 3: Embedding
├─ 75 chunks × 2.5 sec = ~3 minutes
└─ Target: < 5 minutes

Stage 4: Vector store
└─ Target: < 2 seconds

Total pipeline: < 18 minutes ✓ Acceptable

Summarization:
├─ Topic discovery: < 1 minute
├─ Detail extraction: < 3 minutes
├─ Special queries: < 1 minute
├─ Synthesis: < 1 minute
└─ Total: < 6 minutes ✓ Acceptable
```

### 9.4 Test Cases

#### 9.4.1 Cleanup Test Cases

```
Test 1: Basic Backchannel Removal
Input: "We need to upgrade. Yeah. The system works. Mm-hmm."
Expected: "We need to upgrade. [SENTENCE_END] The system works. [SENTENCE_END]"

Test 2: Technical Abbreviation Preservation
Input: "The PLC connects to the VFD per NEC standards."
Expected: "The PLC connects to the VFD per NEC standards. [SENTENCE_END]"

Test 3: Context-Dependent Words
Input: "Turn right at the intersection. Right?"
Expected: "Turn right at the intersection. [SENTENCE_END]"
(Should keep "right" as direction, remove "Right?" as acknowledgment)

Test 4: Filler Removal
Input: "So um the budget is like fifty thousand you know dollars."
Expected: "The budget is fifty thousand dollars. [SENTENCE_END]"

Test 5: Topic Shift Detection
Input: "...budget is approved. [pause] Now moving on to PLC integration..."
Expected: "...budget is approved. [SENTENCE_END] [TOPIC_SHIFT: PLC Integration] ..."
```

#### 9.4.2 Retrieval Test Cases

```
Test 1: Basic Topic Retrieval
Query: "What did we discuss about the budget?"
Expected: Retrieve chunks containing budget discussions
Verify: Top 5 results are all budget-related

Test 2: Decision Retrieval
Query: "What decisions were made?"
Expected: Retrieve chunks with decision language
Verify: Results contain "decided", "agreed", "will", etc.

Test 3: Action Item Retrieval
Query: "What are the action items?"
Expected: Retrieve chunks mentioning tasks/actions
Verify: Results contain "will", "should", "need to", etc.

Test 4: Technical Term Search
Query: "PLC configuration"
Expected: Retrieve PLC-related chunks
Verify: Results mention PLC and configuration

Test 5: Cross-Topic Retrieval
Query: "How does the budget affect the PLC upgrade?"
Expected: Retrieve chunks mentioning both budget and PLC
Verify: Results contain both topics
```

#### 9.4.3 Summarization Test Cases

```
Test 1: Single-Topic Meeting
Input: Meeting discussing only VFD upgrade
Expected: Summary with one main topic section
Verify: No hallucinated topics

Test 2: Multi-Topic Meeting
Input: Meeting with budget, PLC, API discussions
Expected: Summary with 3+ topic sections
Verify: All major topics covered

Test 3: Decision Extraction
Input: Meeting with 5 explicit decisions
Expected: Decisions section lists all 5
Verify: 100% decision capture

Test 4: Action Item Extraction
Input: Meeting with 8 action items
Expected: Action items section lists all 8
Verify: Owners and dates extracted when mentioned

Test 5: Complex Meeting
Input: 2-hour meeting with debates, decisions, actions
Expected: Comprehensive summary covering all aspects
Verify: Structure is clear, content is accurate
```

### 9.5 Acceptance Criteria

```
For ClioEmbedder to be production-ready:
✓ Cleanup accuracy > 90%
✓ Abbreviation preservation > 95%
✓ Processing time < 15 min for 2-hour meeting
✓ Checkpoint/resume works correctly
✓ All output files generated successfully
✓ No crashes or data loss

For Summarization to be production-ready:
✓ Topic coverage > 90%
✓ Decision accuracy > 95%
✓ Action item completeness > 95%
✓ Zero hallucinations
✓ Readability score > 4.0
✓ Processing time < 6 minutes

For ClioAI integration to be production-ready:
✓ Seamless workflow (no manual steps)
✓ Progress reporting works accurately
✓ UI remains responsive
✓ Error handling graceful
✓ Summary opens in browser automatically
✓ User can process multiple meetings in sequence
```

---

## 10. Future Enhancements

### 10.1 ClioRAG (Conversational Query Interface)

**Phase 2 - Standalone Tool for Meeting Queries**

#### 10.1.1 Purpose
Enable conversational querying across all past meetings with natural language understanding and multi-turn dialogue.

#### 10.1.2 Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      ClioRAG                            │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Meeting Selection                                       │
│  ┌────────────────────────────────────────────────┐    │
│  │ • List all available meetings                  │    │
│  │ • Filter by date, participants, keywords       │    │
│  │ • Select single or multiple meetings           │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Query Interface                                         │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Natural language input                       │    │
│  │ • Multi-turn conversation                      │    │
│  │ • Conversation history                         │    │
│  │ • Follow-up questions                          │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Retrieval Agent                                         │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Query understanding (extract intent)         │    │
│  │ • Meeting selection (which meetings relevant)  │    │
│  │ • Chunk retrieval (find relevant content)      │    │
│  │ • Context building (expand & merge)            │    │
│  └────────────────────────────────────────────────┘    │
│                          ↓                              │
│  Response Generation                                     │
│  ┌────────────────────────────────────────────────┐    │
│  │ • Synthesize answer from retrieved chunks      │    │
│  │ • Cite sources (meeting + timestamp)           │    │
│  │ • Suggest follow-up questions                  │    │
│  └────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

#### 10.1.3 Example Interactions

```
User: "What did we decide about the budget in last week's meetings?"

ClioRAG:
  ├─ Filters meetings from last week (3 meetings found)
  ├─ Queries each for budget-related decisions
  ├─ Retrieves and synthesizes
  └─ Response:
      "Based on 3 meetings last week, here are the budget decisions:
       
       1. December 5 Standup: Approved $50K for VFD upgrade in Q1
          [Link to chunk]
       
       2. December 6 Planning: Deferred hiring budget to Q2
          [Link to chunk]
       
       3. December 7 Review: Increased API development budget by 20%
          [Link to chunk]"

User: "Was there any discussion about timeline for #1?"

ClioRAG:
  ├─ Understands "timeline for #1" = VFD upgrade timeline
  ├─ Retrieves context around VFD decision
  └─ Response:
      "Yes, in the December 5 meeting, the team discussed a 6-week
       timeline for VFD upgrade, starting mid-January.
       [Link to chunk]"

User: "Who was assigned to lead that project?"

ClioRAG:
  ├─ Understands "that project" = VFD upgrade
  ├─ Retrieves action items from same meeting
  └─ Response:
      "Moses was assigned to lead the VFD upgrade project, with
       engineering team support. [Link to chunk]"
```

#### 10.1.4 Features

```
Core Features:
├─ Multi-meeting search
├─ Natural language queries
├─ Conversation memory (follow-up questions)
├─ Source citations with links
├─ Timeline view (how topics evolved)
└─ Export conversations

Advanced Features (Future):
├─ Topic tracking (how X changed over time)
├─ Person tracking (what did Alice say about Y?)
├─ Comparison queries (Q1 vs Q2 meetings)
├─ Trend analysis (sentiment, frequency)
└─ Integration with calendar
```

#### 10.1.5 UI Mockup

```
┌──────────────────────────────────────────────────────────┐
│ ClioRAG - Meeting Intelligence                           │
├──────────────────────────────────────────────────────────┤
│                                                           │
│ Meetings: [All] [Last Week] [Last Month] [Custom ▼]     │
│ Found: 15 meetings                                        │
│                                                           │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ 🔍 Ask anything about your meetings...              │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                           │
│ Conversation History:                                     │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ You: What did we discuss about the API upgrade?    │ │
│ │                                                      │ │
│ │ ClioRAG: Based on 2 recent meetings:                │ │
│ │ • Error handling strategy (Dec 5) [View]            │ │
│ │ • Timeline for implementation (Dec 7) [View]        │ │
│ │                                                      │ │
│ │ You: What was the timeline?                         │ │
│ │                                                      │ │
│ │ ClioRAG: 6 weeks starting mid-January, with...     │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                           │
│ Suggested questions:                                      │
│ • Who is leading the API project?                        │
│ • What blockers were mentioned?                          │
│ • Compare with last quarter's API work                   │
└──────────────────────────────────────────────────────────┘
```

### 10.2 Performance Optimizations

**Future optimizations (after MVP):**

```
1. Parallel Processing
   ├─ Process sliding windows in parallel
   ├─ Generate embeddings in parallel (batch API)
   └─ Estimated speedup: 2-3x

2. Quantization Optimization
   ├─ Test lower quantization (Q3, Q4_0)
   ├─ Evaluate quality vs speed tradeoff
   └─ Potential speedup: 1.5-2x

3. Caching Strategy
   ├─ Cache embeddings for reused chunks
   ├─ Cache LLM responses for similar prompts
   └─ Speedup for reprocessing: 10x

4. GPU Optimization
   ├─ Better GPU layer distribution
   ├─ Batch processing for embeddings
   └─ Potential speedup: 2x
```

### 10.3 Feature Additions

**Potential future features:**

```
1. Real-time Processing
   ├─ Generate embeddings during meeting (background)
   ├─ Summary available immediately after stop
   └─ Requires: More efficient pipeline

2. Speaker Diarization
   ├─ Integrate Whisper with speaker detection
   ├─ Attribute content to speakers
   └─ Enables: "What did Alice say about X?"

3. Multi-language Support
   ├─ Use multilingual embedding models
   ├─ Translate summaries
   └─ Enables: International meetings

4. Cloud Sync
   ├─ Backup meetings to cloud
   ├─ Access from multiple devices
   └─ Requires: Privacy considerations

5. Mobile App
   ├─ Record on mobile
   ├─ Process on desktop
   └─ Query from mobile

6. Integration APIs
   ├─ Export to Notion, Confluence
   ├─ Integration with project management tools
   └─ Webhook notifications for new summaries
```

### 10.4 Model Updates

**Model upgrade path:**

```
Current: llama-3.1-8B-Q4
Future options:
├─ llama-3.3-70B-Q4 (if larger VRAM available)
├─ Specialized fine-tuned models (meeting-specific)
├─ Mixture-of-experts models (better quality)
└─ Streaming models (lower latency)

Embedding model:
Current: bge-small-en-v1.5 (384 dims)
Future options:
├─ bge-large (768 dims, better quality)
├─ OpenAI embeddings (API, best quality)
├─ Cohere embeddings (specialized)
└─ Custom fine-tuned embeddings
```

---

## 11. Glossary

**Terms used in this document:**

- **Backchannel**: Short verbal acknowledgments like "yeah", "mm-hmm", "uh-huh" used during conversation
- **Chunk**: A segment of cleaned transcript (300-450 tokens) that serves as a retrieval unit
- **Embedding**: A numerical vector (384 dimensions) representing semantic meaning of text
- **GGUF**: File format for quantized large language models
- **HNSW**: Hierarchical Navigable Small World - algorithm for fast similarity search
- **LlamaSharp**: C# wrapper for llama.cpp, enables running LLMs locally
- **Quantization (Q4, Q8)**: Compression technique for models - Q4 = 4-bit, Q8 = 8-bit
- **RAG**: Retrieval-Augmented Generation - using retrieved content to augment LLM responses
- **Sliding Window**: Processing long text by overlapping fixed-size segments
- **USearch**: High-performance vector similarity search library
- **Vector Store**: Database optimized for storing and searching embedding vectors
- **Whisper**: OpenAI's speech-to-text model used for transcription

---

## 12. References & Resources

**Models:**
- bge-small: https://huggingface.co/BAAI/bge-small-en-v1.5
- llama-3.1: https://huggingface.co/meta-llama/Meta-Llama-3.1-8B-Instruct
- llama-3.2: https://huggingface.co/meta-llama/Llama-3.2-3B-Instruct

**Libraries:**
- LlamaSharp: https://github.com/SciSharp/LLamaSharp
- USearch: https://github.com/unum-cloud/usearch
- Markdig (MD→HTML): https://github.com/xoofx/markdig

**Documentation:**
- Vector embeddings: https://www.pinecone.io/learn/vector-embeddings/
- RAG patterns: https://www.anthropic.com/research/retrieval-augmented-generation
- K-means clustering: https://en.wikipedia.org/wiki/K-means_clustering

---

## Document Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024-12-09 | Assistant | Initial comprehensive requirements document |

---

**END OF REQUIREMENTS DOCUMENT**

---

This document serves as the complete specification for the ClioAI enhancement project. All stakeholders should review and approve before development begins. Questions or clarifications should be documented and added to this living document.