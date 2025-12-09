# Clio AI Software Manual

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Getting Started](#getting-started)
4. [Interface Overview](#interface-overview)
5. [Configuration](#configuration)
   - [General Settings](#general-settings)
   - [Clean Up Settings](#clean-up-settings)
   - [Summarize Settings](#summarize-settings)
6. [Using Clio AI](#using-clio-ai)
   - [Starting a Transcription](#starting-a-transcription)
   - [Stopping a Transcription](#stopping-a-transcription)
   - [Cleaning Up Text](#cleaning-up-text)
   - [Summarizing Content](#summarizing-content)
7. [Troubleshooting](#troubleshooting)
8. [Future Updates](#future-updates)
9. [Legal Information](#legal-information)

## Introduction

Clio AI is an AI-powered Windows application designed to transcribe audio streams into text. Unlike built-in transcription features in conferencing software such as Zoom, Teams, and Meet, Clio AI addresses important privacy and legal concerns by never storing recorded audio files.

The application offers three main functions:
1. Transcribing audio to text
2. Cleaning up the raw transcribed text by removing unwanted backchanneling (such as "ah," "um," and other filler words)
3. Summarizing the cleaned text for quick review

Clio AI was specifically created to address legal restrictions that cause many companies to disable built-in transcription features in conferencing software. Since traditional transcription tools can record audio streams, transcribe them, and identify speakers, they potentially create evidence that could be used in legal proceedings. Clio AI mitigates this risk by immediately deleting audio chunks after transcription and by not identifying speakers.

While Clio AI provides advanced transcription capabilities, we recommend human verification of the final content, as accuracy depends on the underlying language models used.

## Installation

*Note: Detailed installation instructions would be included in the final manual. Based on the provided information, specific installation steps are not available.*

## Getting Started

Before using Clio AI, you'll need:
1. A Windows PC
2. An OpenAI API key for accessing the Whisper transcription engine
3. An OpenAI API key for the cleanup and summarization functions (can be the same key or separate keys)

## Interface Overview

The Clio AI interface is designed to be simple and intuitive. The main window contains:

- **Control Buttons (Top)**: Start, Stop, Clean Up, Summarize, Clear, and Settings
- **Audio Device Selection**: Dropdown menu to select your audio input source
- **Refresh Devices**: Button to update the list of available audio devices
- **Main Text Area**: Displays instructions, transcribed text, and results
- **Status Bar**: Shows audio level and processing status indicators

## Configuration

To configure Clio AI, click the "Settings" button in the main interface. This opens a new window with four tabs:

### General Settings

In the General tab, you'll configure:

1. **Whisper API Key**: Your OpenAI API key required to access the Whisper transcription engine.
   - Enter the key in the provided field.
   - Use the "Show" checkbox to reveal the key if needed.

2. **Chunk Duration**: Adjust how long each audio segment should be before processing.
   - Default: 30 seconds
   - Use the slider or enter a value directly
   - Shorter durations provide faster feedback but may increase API costs
   - Longer durations might improve context for transcription but delay feedback

3. **Default Save Path**: Set where transcribed files will be saved.
   - Type directly or use the browse button to select a folder

### Clean Up Settings

In the Clean Up tab, you'll configure:

1. **OpenAI API Key**: Your key for accessing the LLM that will clean up the transcribed text.
   - This can be a different key than the one used for transcription if you want to track costs separately.

2. **Cleanup Model**: Select which OpenAI model to use for text cleanup.
   - You can choose a less expensive model as cleanup doesn't require as much intelligence as summarization.

3. **System Prompt**: Instructions for the LLM on how to clean up the text.
   - The default prompt is adequate for most users
   - Can be customized for specific needs or terminology

### Summarize Settings

In the Summarize tab, you'll configure:

1. **OpenAI API Key**: Your key for accessing the LLM that will summarize the cleaned text.
   - This can be a different key than those used for transcription or cleanup.

2. **Summarize Model**: Select which OpenAI model to use for text summarization.
   - Recommendation: Use your most capable model for this task as it requires more intelligence.

3. **System Prompt**: Instructions for the LLM on how to summarize the text.
   - The default prompt works well for most situations
   - Can be customized based on your specific needs

### About Tab

The About tab provides information about the software version and licensing details.

## Using Clio AI

### Starting a Transcription

1. Configure settings if you haven't already done so.
2. Select the audio device from the dropdown menu.
3. Click the "Start" button to begin transcription.
   - The elapsed time will display in the status bar at the bottom right.
   - A red progress bar indicates that transcription is in session.
   - The Audio Level bar will become active only when transcription is in session AND audio is being detected through the selected device.
   - If no Audio Level activity is shown despite sound being played, you may have selected the wrong audio device.
4. The text will appear in the main window after the first chunk of audio is processed.

To switch to another audio device during use:
1. Click the "Stop" button to end the current transcription session
2. Select the new audio device from the dropdown menu
3. Click "Start" again to begin transcription with the new device
4. Verify the Audio Level bar responds to audio to confirm the correct device is now being used

### Stopping a Transcription

1. Click the "Stop" button when you want to end the transcription.
2. The total elapsed time will be displayed in the status bar.
3. The transcribed text will be automatically saved to your default save path.
   - A new directory named with the current date and time (format: yyyymmdd_hhmmss) will be created in your specified working directory.
   - The raw transcription will be saved as "transcription.txt" in this directory.

### Cleaning Up Text

1. After transcription is complete, click the "Clean Up" button.
2. A blue progress bar in the status bar indicates that cleanup is in progress.
3. For longer recordings, this process may take several minutes.
4. The cleaned text will replace the raw transcription in the main window.
5. A file named "cleaned.txt" will be automatically saved in the same directory as your transcription file.

*Note: You can skip this step if you're transcribing professional audio that doesn't contain backchanneling, such as audiobooks or professionally produced shows.*

### Summarizing Content

1. After cleaning up the text (or directly after transcription if cleanup wasn't needed), click the "Summarize" button.
2. A blue progress bar in the status bar indicates that summarization is in progress.
3. For longer content, this process may take several minutes.
4. When summarization is complete:
   - A file named "summary.md" will be saved in the same directory as your transcription and cleaned files
   - A web browser will automatically launch to display a well-formatted version of the summary
   - The browser display includes a convenient Copy button at the top, allowing you to easily copy the formatted text for use in emails, documents, or other applications
5. The summary will also be displayed in the main application window.

### Clearing the Text Box

1. Click the "Clear" button to remove all text from the main window.
2. This allows you to start a new transcription session.

## Troubleshooting

*Note: Specific troubleshooting guidance would be included in the final manual. Based on the provided information, detailed troubleshooting steps are not available.*

Common issues might include:
- API key validation errors
- Audio device selection problems
- File saving errors

## Future Updates

The Clio AI development team is working on several exciting features for future releases:

1. **Integrated Whisper Engine**: Eliminate the need for external API calls for enhanced privacy.
2. **Ollama Local LLM Support**: Process data locally without requiring cloud APIs.
3. **Rate Limiting of API Usage**: Better control over costs.
4. **Additional LLM Suppliers**: Support for Claude, X AI, OpenRouter, and others.
5. **Microphone Mixing Feature**: Adjust microphone audio levels within the application.
6. **Microsoft Calendar Event Trigger**: Automate transcription based on your calendar.

## Legal Information

Clio AI is released under the MIT License. The source code is available on GitHub.

This software was created with AI assistance. The Function Description Specification (FDS), Product Requirement Document (PRD), and Software Development Plan (SDP) were all generated with AI tools and then implemented by a coding AI agent (Cline), guided by human oversight.

---

© 2025 Clio AI. All rights reserved.
