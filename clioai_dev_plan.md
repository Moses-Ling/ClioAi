# ClioAI v1.4 Development Plan - AI Coding Agent

## Project Overview

**Objective**: Implement Cloud/Local switching functionality for transcription, cleanup, and summarization services, along with an optional export footer feature for ClioAI application.

**Key Goals**:
- Enable users to switch between Cloud (OpenAI) and Local services
- Maintain backward compatibility
- Minimize backend changes
- Add promotional export footer functionality

## Technical Scope

### Core Features
1. **Cloud/Local Service Switching**
   - Transcription (Whisper) service routing
   - Cleanup service routing  
   - Summarization service routing
   - Independent configuration per service

2. **Export Footer Enhancement**
   - Optional footer appending to TXT/MD/HTML exports
   - Dynamic model name substitution

3. **UI/UX Improvements**
   - Mode selection controls in appropriate tabs
   - Conditional field enabling/disabling
   - Service connectivity testing

## Architecture Requirements

### Settings Schema Extensions
```json
{
  "Export": {
    "AppendFooter": "boolean"
  },
  "Whisper": {
    "UseLocalWhisper": "boolean",
    "WhisperHost": "string",
    "WhisperPort": "integer", 
    "WhisperModelsEndpointPath": "string (/v1/models default)",
    "WhisperModel": "string (whisper-1 default)"
  },
  "Cleanup": {
    "UseLocalCleanup": "boolean",
    "CleanupHost": "string",
    "CleanupPort": "integer"
  },
  "Summarize": {
    "UseLocalSummarize": "boolean", 
    "SummarizeHost": "string",
    "SummarizePort": "integer"
  }
}
```

### Service Routing Logic
- **TranscriptionService**: Route between `https://api.openai.com` (Cloud) and `http(s)://{host}:{port}` (Local)
- **OpenAiChatService**: Configure base URL and authentication headers per request
- **Export Service**: Conditionally append footer with model substitution

## Phase-Based Implementation Plan

### Phase 1: UI Framework & Settings (Sprint 1)
**Duration**: 5-7 days

**Tasks**:
1. **Settings UI Components**
   - Create radio button controls for Cloud/Local mode selection
   - Implement conditional field visibility logic
   - Add host/port input fields with validation
   - Design test connection buttons

2. **Tab Layout Updates**
   - **General Tab**: Add Transcription mode controls
   - **Clean Up Tab**: Add cleanup service mode controls  
   - **Summarize Tab**: Add summarization mode controls
   - **Export Section**: Add footer toggle control

3. **Settings Persistence**
   - Extend settings schema with new configuration keys
   - Implement save/load functionality for new settings
   - Add default value initialization

**Acceptance Criteria**:
- [ ] Mode switching toggles Cloud/Local field availability
- [ ] Settings persist across application restarts
- [ ] UI shows appropriate fields based on selected mode
- [ ] Input validation prevents invalid host/port combinations

### Phase 2: Service Architecture (Sprint 2)  
**Duration**: 7-10 days

**Tasks**:
1. **TranscriptionService Enhancement**
   - Implement base URL switching logic
   - Add authentication header management
   - Configure model parameter routing
   - Ensure backward compatibility

2. **Chat Service Routing**
   - Modify OpenAiChatService for dynamic base URL configuration
   - Implement per-request authentication setup
   - Add cleanup/summarize service differentiation
   - Maintain existing API contracts

3. **Configuration Management**
   - Create service factory pattern for Cloud/Local instances
   - Implement configuration validation
   - Add error handling for invalid configurations

**Acceptance Criteria**:
- [ ] Services correctly route to Cloud or Local endpoints
- [ ] Authentication headers set appropriately per mode
- [ ] Model parameters passed correctly to Local services
- [ ] Existing functionality remains unaffected

### Phase 3: Connectivity & Testing (Sprint 3)
**Duration**: 3-5 days

**Tasks**:
1. **Connection Testing Framework**
   - Implement GET requests to `/v1/models` endpoint
   - Create success/error dialog components
   - Add timeout and error handling logic
   - Design user feedback mechanisms

2. **Local Service Integration**
   - Test Local Whisper connectivity via models endpoint
   - Test Local Cleanup service connectivity
   - Test Local Summarize service connectivity
   - Implement model name auto-population from API responses

3. **Error Handling & Validation**
   - Add network connectivity error handling
   - Implement service availability validation
   - Create user-friendly error messages
   - Add retry mechanisms for failed connections

**Acceptance Criteria**:
- [ ] Test buttons provide clear success/failure feedback
- [ ] Model names auto-populate from Local service responses
- [ ] Error messages guide users toward resolution
- [ ] Connection timeouts handled gracefully

### Phase 4: Export Footer Feature (Sprint 4)
**Duration**: 2-3 days

**Tasks**:
1. **Footer Template System**
   - Create configurable footer template
   - Implement model name substitution logic
   - Design footer formatting for TXT/MD/HTML formats

2. **Export Integration**
   - Modify export routines to conditionally append footer
   - Ensure footer appears only when enabled
   - Test across all export formats (TXT/MD/HTML)

3. **Footer Content Management**
   - Implement dynamic model name detection
   - Add ClioAI branding and URL
   - Ensure proper formatting across export types

**Acceptance Criteria**:
- [ ] Footer appends only when setting enabled
- [ ] Model name correctly substituted in footer text
- [ ] Footer formatting appropriate for each export format
- [ ] Toggle setting controls footer inclusion

## Quality Assurance Plan

### Testing Strategy
1. **Unit Tests**
   - Service routing logic validation
   - Settings persistence testing
   - UI component behavior verification
   - Export footer functionality testing

2. **Integration Tests**
   - Cloud service connectivity testing
   - Local service mock server testing
   - End-to-end workflow validation
   - Cross-platform compatibility testing

3. **User Acceptance Testing**
   - Mode switching user experience
   - Service configuration workflows  
   - Export functionality with/without footer
   - Error handling user experience

### Performance Considerations
- Minimize API calls during configuration testing
- Implement connection pooling for Local services
- Cache model lists to reduce repeated API calls
- Optimize UI responsiveness during service switching

## Risk Assessment & Mitigation

### Technical Risks
1. **Backward Compatibility**
   - *Risk*: Breaking existing Cloud service functionality
   - *Mitigation*: Comprehensive regression testing, feature flags

2. **Local Service Reliability**
   - *Risk*: Inconsistent Local service API implementations
   - *Mitigation*: Robust error handling, clear user documentation

3. **Configuration Complexity**
   - *Risk*: User confusion with multiple service configurations
   - *Mitigation*: Intuitive UI design, helpful error messages

### Operational Risks
1. **Service Discovery**
   - *Risk*: Users unable to configure Local services correctly
   - *Mitigation*: Auto-discovery features, configuration validation

2. **Support Burden**
   - *Risk*: Increased support requests for Local service setup
   - *Mitigation*: Comprehensive documentation, troubleshooting guides

## Success Metrics

### Functional Metrics
- [ ] 100% backward compatibility maintained
- [ ] All three services (Transcription, Cleanup, Summarize) support Cloud/Local switching
- [ ] Export footer functionality working across all formats
- [ ] Zero critical bugs in production release

### User Experience Metrics
- [ ] Intuitive mode switching (< 3 clicks to change modes)
- [ ] Clear error messaging (user can resolve 80% of issues independently)
- [ ] Fast service switching (< 2 seconds for mode changes)

## Deployment Strategy

### Pre-Release Checklist
- [ ] All unit tests passing
- [ ] Integration tests validated
- [ ] Performance benchmarks met
- [ ] Documentation updated
- [ ] User acceptance testing completed

### Release Plan
1. **Beta Release**: Internal testing with limited user group
2. **Staging Deployment**: Full feature testing in production-like environment  
3. **Production Release**: Gradual rollout with monitoring
4. **Post-Release**: Monitor usage patterns and gather user feedback

## Post-Implementation Considerations

### Future Enhancements
1. **HTTPS Support**: Add toggle for secure Local service connections
2. **Service Discovery**: Auto-detect Local services on network
3. **Configuration Profiles**: Save/load service configuration presets
4. **Advanced Testing**: Expanded connectivity diagnostics

### Maintenance Plan
- Regular compatibility testing with OpenAI API updates
- Local service integration documentation updates
- User feedback incorporation for UX improvements
- Performance monitoring and optimization

---

*This development plan provides a structured approach to implementing ClioAI v1.4 features while maintaining code quality, user experience, and system reliability.*