# CODEDOTNET_PROJECT_APPROACH.md

# CodeDotNet Platform
## Project Approach, Strategy, Execution Plan and Development Roadmap

Version: 1.0

Project: CodeDotNet

Existing Product:
Browser-based C# playground powered by Roslyn and .NET 10 WebAssembly

Target Outcome:
Transform CodeDotNet into a reusable execution platform supporting:

- Browser Playground
- MCP Integration
- AI Agent Execution
- Future CLI Support
- Future IDE Integration

Target Engineering Level:
Senior Software Engineer III (SDE3)

Hosting Cost:
$0

Authentication:
None

Accounts:
None

Server Infrastructure:
None

Primary Languages:

- C#
- TypeScript

Technology Stack:

- .NET 10 WebAssembly
- Roslyn
- TypeScript
- MCP
- GitHub Pages
- GitHub Actions
- npm

---

# Executive Summary

The goal is not to build another browser playground.

The goal is not to build another MCP server.

The goal is to evolve CodeDotNet into a reusable execution platform with multiple hosts sharing a common execution engine.

The most valuable asset already exists:

- Roslyn Integration
- .NET WebAssembly Runtime
- Browser Execution
- GitHub Pages Deployment

These existing capabilities must be preserved and expanded.

The project should be approached as a platform engineering initiative rather than a feature addition.

The entire architecture should revolve around a shared execution engine.

Browser support becomes one host.

MCP becomes another host.

Future integrations become additional hosts.

---

# Long-Term Product Vision

CodeDotNet should ultimately become:

> A local-first C# execution platform powered by Roslyn and .NET WebAssembly that can be consumed from browsers, AI agents, command line tools, and future developer tooling without requiring cloud infrastructure or user accounts.

---

# High-Level Product Positioning

Current Position:

```text
Browser Playground
```

Target Position:

```text
CodeDotNet Platform

├── Browser Playground
├── MCP Host
├── Shared Execution Engine
├── Security Layer
├── Testing Framework
├── Documentation Portal
└── Future Extensions
```

---

# Core Development Philosophy

## Principle 1

Execution logic exists only once.

There must never be separate compilation logic for:

- Browser
- MCP
- CLI

All hosts share the same implementation.

---

## Principle 2

Hosts consume the platform.

Hosts do not implement compiler logic.

Hosts communicate through contracts.

---

## Principle 3

Platform before features.

Every major feature should be evaluated based on whether it increases platform capability.

---

## Principle 4

No paid infrastructure.

The architecture must remain executable entirely on the user's machine.

---

## Principle 5

No accounts.

No authentication.

No login.

No registration.

No API keys.

---

## Principle 6

Local-first design.

User code should remain on the user's machine.

---

## Principle 7

Security through isolation.

Assume input code is malicious.

---

## Principle 8

Every architectural decision must be documented.

---

# Stage 1
# Existing CodeDotNet Discovery

Before any implementation work begins a complete inventory of the existing system must be created.

---

## Objectives

Understand:

- Current architecture
- Runtime flow
- Compilation flow
- Execution flow
- Dependency graph
- Build system
- Deployment process

---

## Deliverables

### CURRENT_ARCHITECTURE.md

Document:

- Repository layout
- Component diagram
- Runtime diagram
- Deployment diagram
- Build process

---

### CODE_INVENTORY.md

Classify every major component.

Categories:

#### Browser Layer

Examples:

- Monaco
- React
- HTML
- CSS
- Themes
- Layout

---

#### Compilation Layer

Examples:

- Syntax Tree Creation
- Metadata References
- Roslyn Compilation
- Diagnostics

---

#### Execution Layer

Examples:

- Assembly Load
- Reflection
- Runtime Bootstrap

---

#### Infrastructure Layer

Examples:

- Deployment
- Build Scripts
- GitHub Actions

---

### DEPENDENCY_MAP.md

Document:

- NuGet dependencies
- npm dependencies
- Runtime dependencies

---

# Stage 2
# Target Architecture Design

Design the future architecture before modifying any code.

---

## Required Diagrams

### Context Diagram

```text
User
  │
  ▼

Hosts
  │
  ▼

Execution Platform
  │
  ▼

Runtime
```

---

### Container Diagram

```text
Browser Host
MCP Host
Execution Engine
Contracts
Runtime
```

---

### Component Diagram

```text
Validation
Compilation
Execution
Diagnostics
Policies
Output Capture
```

---

### Sequence Diagrams

Create:

- Validation Flow
- Compilation Flow
- Execution Flow
- Timeout Flow
- Failure Flow
- Cancellation Flow

---

## Deliverables

```text
TARGET_ARCHITECTURE.md
```

```text
ARCHITECTURE_DECISIONS.md
```

---

# Stage 3
# Shared Execution Engine

Extract all shared execution behavior.

This becomes the foundation of the platform.

---

## Responsibilities

Validation

Compilation

Execution

Diagnostics

Capability Management

Test Execution

Policy Enforcement

---

## Non Responsibilities

UI

Editor

MCP

Transport

Layout

Theme

Website

---

## Deliverables

Create:

```text
CodeDotNet.Engine
```

---

## Public Surface

The engine should expose:

```text
Validate
Compile
Execute
RunTests
```

through stable contracts.

---

# Stage 4
# Stable Contracts

Create a shared contracts layer.

All communication happens through contracts.

No host may directly consume internal engine types.

---

## Request Contracts

ValidationRequest

CompilationRequest

ExecutionRequest

TestRequest

---

## Response Contracts

ValidationResult

CompilationResult

ExecutionResult

TestResult

---

## Diagnostic Contracts

Error

Warning

Info

Metrics

---

## Metadata Contracts

Capabilities

Versions

Limits

Restrictions

---

# Stage 5
# Browser Refactor

Refactor the browser application to consume the engine.

---

## Browser Responsibilities

Editor

Monaco

Output UI

Themes

Layout

Sharing

GitHub Pages Hosting

---

## Browser Non Responsibilities

Compilation

Runtime Management

Roslyn Configuration

Execution Policies

Assembly References

Security Rules

---

## Outcome

Browser becomes a host.

Browser no longer owns compiler logic.

---

# Stage 6
# Platform Documentation

Create a documentation-first project.

---

## Documentation Areas

Architecture

Security

Benchmarks

Deployment

Testing

Contributing

Roadmap

---

## Required Documents

README.md

TARGET_ARCHITECTURE.md

SECURITY_MODEL.md

THREAT_MODEL.md

CONTRIBUTING.md

ROADMAP.md

BENCHMARKS.md

---

# Stage 7
# MCP Strategy

MCP is introduced only after the engine is stable.

---

## Positioning

MCP is not a separate product.

MCP is a new host.

---

## Architecture

```text
Claude
Cursor
VS Code

      │

      ▼

MCP Host

      │

      ▼

Execution Engine

      │

      ▼

Roslyn Runtime
```

---

## MCP Responsibilities

Tool Registration

Schema Validation

Request Routing

Worker Management

Error Mapping

Response Serialization

Logging

---

## MCP Non Responsibilities

Compilation

Runtime Behaviour

Diagnostics Creation

Policy Logic

Execution Logic

---

# Stage 8
# Isolation Strategy

This stage is critical.

The execution environment must be isolated from the MCP process.

---

## Required Architecture

```text
Supervisor Process

        │

        ▼

Worker Process

        │

        ▼

Execution Engine

        │

        ▼

WebAssembly Runtime
```

---

## Reasons

Infinite loops

Excessive memory usage

Broken runtime state

Unhandled exceptions

Assembly accumulation

Output flooding

must never destroy the MCP host.

---

## Worker Lifecycle

Create

Initialize

Execute

Return Result

Dispose

or

Recycle

---

# Stage 9
# Security Strategy

Security must be treated as a first-class architectural concern.

---

## Threat Categories

Resource Abuse

Capability Abuse

State Leakage

Protocol Corruption

Supply Chain Risks

---

## Resource Abuse

Infinite Loop

Recursion Bomb

Memory Bomb

Output Flooding

Large Source Payloads

---

## Capability Abuse

Filesystem Access

Network Access

Environment Variables

Process Execution

Native Libraries

Dynamic Loading

---

## State Leakage

Static Members

Global State

Console Redirection

Request Contamination

---

## Protocol Corruption

Forged JSON-RPC Messages

Output Injection

Malformed Responses

---

# Stage 10
# Resource Governance

Every execution must have limits.

---

## Time Limits

Execution Timeout

Compilation Timeout

Queue Timeout

---

## Memory Limits

Runtime Memory

Output Memory

Source Size

Request Size

---

## Concurrency Limits

Worker Count

Queue Depth

Requests Per Worker

---

## Recycling Policies

Request Count

Time Based

Failure Based

Memory Based

---

# Stage 11
# Capability Model

Define what is allowed.

Define what is denied.

Document explicitly.

---

## Allowed

Algorithms

Collections

LINQ

Text Processing

Basic Numeric Operations

Console Output

In-Memory Data Structures

---

## Denied

Filesystem

Network

Processes

Native Libraries

External Executables

Environment Secrets

Dynamic Downloads

External Package Installation

---

## Publish Capability Matrix

Every capability must be documented.

---

# Stage 12
# Testing Strategy

Testing must be comprehensive.

---

## Unit Tests

Contracts

Policies

Diagnostics

Validation Rules

Limit Enforcement

---

## Integration Tests

Compilation

Execution

Diagnostics

Runtime Startup

Worker Lifecycle

---

## Security Tests

Infinite Loops

Memory Exhaustion

Output Exhaustion

Filesystem Attempts

Network Attempts

Process Attempts

Native Interop Attempts

---

## MCP Tests

Tool Discovery

Tool Execution

Schema Validation

Cancellation

Protocol Integrity

---

## Soak Tests

Thousands of Executions

Worker Recycling

Repeated Failures

Long Duration Runs

---

# Stage 13
# Benchmarking Strategy

Everything should be measured.

---

## Measurements

Runtime Startup

Compilation Time

Execution Time

Memory Consumption

Worker Startup

Worker Replacement

MCP Latency

---

## Reporting

Publish:

p50

p95

p99

Memory Usage

Environment Details

Version Details

---

# Stage 14
# GitHub Pages Strategy

GitHub Pages remains the primary host.

---

## Purpose

Browser Playground

Documentation

Architecture

Benchmarks

Security Information

Setup Guides

Roadmaps

---

## Hosted Areas

Landing Page

Playground

Architecture Explorer

Documentation Portal

Benchmark Reports

Security Reports

MCP Setup Guides

---

# Stage 15
# GitHub Actions Strategy

Use GitHub Actions as the automation backbone.

---

## Build Pipeline

Build

Test

Package

Verify

Deploy

---

## Security Pipeline

Dependency Scan

Vulnerability Scan

License Validation

---

## Quality Pipeline

Lint

Formatting

Unit Tests

Integration Tests

---

## Documentation Pipeline

Generate

Build

Publish

---

# Stage 16
# Open Source Readiness

The repository should feel like a mature open source project.

---

## Requirements

Clear README

Architecture Documentation

Contribution Guidelines

Issue Templates

PR Templates

Roadmap

Security Policy

License

---

## Community Experience

Easy Onboarding

Clear Setup

Transparent Roadmap

Good Examples

---

# Stage 17
# Portfolio Positioning

The final project should demonstrate:

Compiler Engineering

Roslyn

WebAssembly

Platform Design

System Architecture

MCP

Developer Tooling

Security Engineering

Testing

CI/CD

Documentation

Open Source Ownership

---

## Resume Positioning

CodeDotNet should be presented as:

> Designed and built a local-first C# execution platform powered by Roslyn and .NET WebAssembly supporting browser-based development and AI-agent execution through MCP while requiring no cloud infrastructure, user accounts, or paid hosting.

---

# Stage 18
# Final Success Criteria

The project is considered complete when:

- Existing browser experience remains operational.
- Shared execution engine exists.
- Browser consumes the engine.
- MCP consumes the engine.
- No duplicated execution logic exists.
- Security boundaries are documented.
- Resource limits are enforced.
- Documentation site is completed.
- GitHub Pages remains the primary hosting mechanism.
- Hosting cost remains zero.
- Authentication remains unnecessary.
- Architecture is clearly demonstrated.
- Security testing is automated.
- CI/CD is automated.
- Benchmarks are published.
- The repository clearly demonstrates SDE3-level ownership, architecture, and engineering depth.

---

# Final Guiding Rule

Every future feature should answer the following question:

"Does this strengthen CodeDotNet as a reusable execution platform?"

If the answer is yes:

Build it.

If the answer is no:

Re-evaluate the design.

The execution engine is the core asset.

Everything else is a host around it.