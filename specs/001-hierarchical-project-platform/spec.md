# Feature Specification: Hierarchical Project & Work Management Platform

**Feature Branch**: `001-hierarchical-project-platform`

**Created**: 2026-08-31

**Status**: Draft

**Input**: User description: Constitution-driven enterprise platform for hierarchical project management, work decomposition, document lifecycle, LLM-assisted processing, and full auditability.

## User Scenarios & Testing

### User Story 1 - Organize Work Across Hierarchical Teams (Priority: P1)

A Chief Manager creates an organization, builds a management hierarchy, and decomposes enterprise goals into projects, work items, and subtasks assignable to subordinate managers and employees. Each user sees only the resources within their authorization boundary.

**Why this priority**: This is the core value proposition. Without hierarchy-aware organization and work decomposition, the platform has no purpose. Everything else (documents, metrics, LLM, audit) builds on top of this foundation.

**Independent Test**: A single manager can create an organization unit, add two subordinate managers, create one project, create five work items in that project, assign them to the subordinates, and verify each subordinate sees only their assigned items.

**Acceptance Scenarios**:

1. **Given** a root manager is logged in, **When** they create an organization unit named "Engineering", **Then** the unit is created with the root manager as its head and appears in their organizational view
2. **Given** an organization unit exists, **When** the root manager creates a subordinate manager under it, **Then** the new manager's organizational path reflects the hierarchy
3. **Given** a hierarchy exists, **When** a subordinate manager creates a project, **Then** the project is scoped to their organization unit
4. **Given** a project with work items assigned to multiple users, **When** a subordinate manager views the project, **Then** they see only work items within their authorization boundary
5. **Given** a work item exists, **When** a manager assigns it to a subordinate, **Then** the subordinate receives the assignment and can act on the item

---

### User Story 2 - Track Work Progress with Configurable Metrics (Priority: P2)

A project owner defines custom metrics for their project (e.g., "content completeness at 40%", "subtask completion at 30%"), and the platform calculates an explainable progress percentage for each work item, showing exactly which indicators contributed to the score.

**Why this priority**: Progress tracking is a primary differentiator from simple Kanban tools. The explainable, configurable nature is a core constitutional principle.

**Independent Test**: A project owner defines two weighted metrics for a project, updates their values, and verifies the work item progress reflects the weighted calculation with a breakdown explanation.

**Acceptance Scenarios**:

1. **Given** a project with configurable metrics defined, **When** a work item's subtasks change state, **Then** the work item's progress percentage updates automatically
2. **Given** a work item has a progress percentage, **When** a user views the progress details, **Then** they see an explanation of which indicators contributed and their weights
3. **Given** a project metric configuration, **When** an owner modifies the weights, **Then** all work items recalculate progress based on the new configuration
4. **Given** a progress calculation, **When** the underlying data changes, **Then** the system retains a record of the previous calculation for traceability

---

### User Story 3 - Manage Documents with Full Lifecycle and Versioning (Priority: P2)

A document owner uploads a file, the system classifies it, indexes it for search, and optionally processes it with LLM assistance. Document versions are immutable once published. The owner reviews and approves or rejects any AI-generated content derived from the document.

**Why this priority**: Documents are first-class domain objects per the constitution. The lifecycle, versioning, and AI traceability requirements are unique to this platform.

**Independent Test**: A user uploads a document, verifies it goes through classification and indexing, creates a corrected version, and confirms the original version remains immutable.

**Acceptance Scenarios**:

1. **Given** a user uploads a document, **When** the upload completes, **Then** the document enters the "Uploaded" state and a processing job is created
2. **Given** a document exists, **When** a corrected version is uploaded, **Then** a new version is created and the previous version remains immutable
3. **Given** a document has been processed by LLM, **When** a reviewer views the result, **Then** the result shows provenance (source document, model, prompt, timestamp)
4. **Given** an AI result is pending review, **When** a reviewer approves it, **Then** the result becomes authoritative and the document state advances
5. **Given** an AI result is pending review, **When** a reviewer rejects it, **Then** the result is marked as rejected and the document re-enters processing queue

---

### User Story 4 - Search Across All Resources with Authorization Filtering (Priority: P3)

A user searches for work items, documents, or projects and receives results filtered by their authorization boundary. Search covers work item titles, descriptions, tags, document metadata, and indexed document content.

**Why this priority**: Search is the primary discovery mechanism. Without authorization filtering, the platform violates the constitutional requirement that users cannot access data outside their boundary.

**Independent Test**: Two users with different authorization levels search for the same term and receive appropriately filtered results.

**Acceptance Scenarios**:

1. **Given** a user performs a search, **When** results are returned, **Then** all results are within the user's authorization boundary
2. **Given** a user searches for a document by content, **When** results are returned, **Then** only indexed and available documents appear
3. **Given** a user searches for work items, **When** results are returned, **Then** results are sorted by relevance and filterable by status, priority, and assignee

---

### User Story 5 - Audit Every Significant Action (Priority: P2)

Every security-sensitive and business-significant action generates an append-only audit record including who did what, when, from where, and the before/after state. The audit trail is tamper-evident.

**Why this priority**: Auditability is ranked second in constitutional priority (only below security). Without it, the platform cannot meet its enterprise-grade accountability requirement.

**Independent Test**: A user performs a series of actions (create project, assign task, change status), and an auditor retrieves the complete audit trail showing the exact sequence with before/after states.

**Acceptance Scenarios**:

1. **Given** a user performs a security-sensitive action, **When** the action completes, **Then** an audit record is created with user, timestamp, action type, resource, and before/after state
2. **Given** an audit record exists, **When** a user with audit access views it, **Then** they see the complete record
3. **Given** a sequence of related actions, **When** an auditor queries by correlation ID, **Then** all related audit records are returned in chronological order

---

## Clarifications

### Session 2026-08-31

- Q: Which authentication flow with oroidentityserver? → A: Authorization Code with PKCE
- Q: How are documents stored? → A: Object storage (Blob/MinIO)
- Q: What search backend is used? → A: Vector embedding search (pgvector/SQL Server vector)
- Q: Which database platform? → A: PostgreSQL with EF Core (native pgvector support)
- Q: LLM processing mode — sync or async? → A: Async for all LLM operations; user notified upon completion

## Edge Cases

- What happens when a manager is removed from the hierarchy while they own active work items? Work items transfer to the manager's parent.
- How does the system handle concurrent edits to the same work item? Optimistic concurrency with version tokens; stale edits are rejected with a meaningful error.
- What happens when the LLM provider is unavailable? Processing jobs enter a "Failed" state with retry scheduling.
- How are documents handled when a user's access is revoked mid-processing? Processing continues; access is enforced at retrieval time.
- What happens when a document version's checksum does not match the stored hash? The version is rejected as corrupted.

## Requirements

### Functional Requirements

- **FR-001**: System MUST persist all business resources in PostgreSQL with EF Core, using optimistic concurrency control
- **FR-002**: System MUST support hierarchical organization units with unlimited depth and managers who can manage other managers
- **FR-002**: System MUST evaluate authorization based on identity, role, organizational position, resource ownership, management ancestry, project membership, explicit grants, and resource sensitivity
- **FR-003**: Users MUST be able to create projects scoped to their organization unit with configurable work item types and states
- **FR-004**: System MUST support work item decomposition into subtasks with dependencies, assignments, priority, criticality, due dates, and estimated effort
- **FR-005**: System MUST calculate work item progress from configurable weighted indicators and retain the calculation explanation
- **FR-006**: System MUST enforce explicit state transitions on work items with authorization checks and audit logging for each transition
- **FR-007**: System MUST manage documents as first-class domain objects with identity, classification, lifecycle states, versioning, checksums, and access history; binary content stored in object storage (Blob/MinIO)
- **FR-008**: Document versions MUST be immutable after publication; corrections create new versions
- **FR-009**: System MUST process documents with LLM providers through a provider-agnostic interface, generating traceable results with full provenance
- **FR-010**: LLM-generated results MUST enter a "Pending Review" state and require human approval before becoming authoritative, unless the operation type permits automatic acceptance
- **FR-011**: System MUST search work items, documents, and projects with results always filtered by the user's authorization boundary; indexing uses vector embedding search (pgvector/SQL Server vector) for semantic document content search
- **FR-012**: System MUST generate append-only, tamper-evident audit records for all security-sensitive and business-significant actions
- **FR-013**: System MUST support configurable project and task metrics with documented weights and calculation rules
- **FR-014**: System MUST persist all business resources with optimistic concurrency control and reject stale updates
- **FR-015**: All LLM operations MUST execute asynchronously via background processing; user notified upon completion
- **FR-016**: Long-running operations (document extraction, indexing, bulk imports) MUST execute asynchronously via background processing
- **FR-017**: System MUST expose stable API contracts with defined request/response models, validation rules, error formats, pagination, filtering, and sorting
- **FR-018**: System MUST integrate with the existing oroidentityserver instance using Authorization Code with PKCE flow for OpenID Connect authentication, supporting OAuth 2.0 access tokens, refresh tokens, claims, roles, and scopes
- **FR-019**: Every production service MUST expose logs, metrics, traces, health checks, and correlation identifiers for distributed tracing

### Key Entities

- **Organization**: Top-level container with users, units, and policies. Attributes: id, name, description, created_by, created_at.
- **OrganizationUnit**: Hierarchical unit within an organization. Attributes: id, name, parent_unit_id, head_id, path (computed from ancestry), metadata.
- **ManagementRelationship**: Links a manager to their subordinates. Attributes: id, manager_id, subordinate_id, effective_from, effective_to, status.
- **User**: Identity within the system linked to oroidentityserver. Attributes: id, external_id, name, email, organization_unit, roles.
- **Project**: Container for work items within an organization unit. Attributes: id, name, description, organization_unit_id, owner_id, metrics_configuration, status, created_at.
- **WorkItem**: A unit of work with hierarchical decomposition. Attributes: id, project_id, parent_id, title, description, type, status, priority, criticality, assignees, due_date, estimated_effort, actual_effort, progress, progress_explanation, tags, evidence, created_by, created_at, updated_at.
- **WorkItemMetric**: Configurable metric definition for a project. Attributes: id, project_id, name, weight, calculation_rule, description.
- **Milestone**: Key date or deliverable in a project. Attributes: id, project_id, title, target_date, status, related_work_items.
- **Document**: First-class domain object with lifecycle. Attributes: id, classification, owner_id, security_classification, checksum, content_type, indexing_state, processing_state, created_at, updated_at.
- **DocumentVersion**: Immutable snapshot of document content. Attributes: id, document_id, version_number, checksum, content_url, published_at, published_by.
- **DocumentClassification**: Category and tags applied to a document. Attributes: id, document_id, category, tags, confidence, classified_by.
- **DocumentAccess**: Record of document access events. Attributes: id, document_id, user_id, accessed_at, access_type.
- **DocumentProcessingJob**: Background job for document processing. Attributes: id, document_id, operation_type, status, started_at, completed_at, error.
- **DocumentIndex**: Search index entry for a document. Attributes: id, document_id, indexed_content, metadata, indexed_at.
- **LlmOperation**: Record of an LLM processing request. Attributes: id, document_id, source_version, operation_type, model, provider, prompt_version, status, result_url, confidence.
- **LlmResult**: Generated output from an LLM operation. Attributes: id, operation_id, content, approved_by, approved_at, rejected_by, rejected_at, rejection_reason.
- **AuditEntry**: Immutable record of a significant action. Attributes: id, actor_id, action_type, resource_type, resource_id, correlation_id, before_state, after_state, timestamp, ip_address.
- **Notification**: User notification about relevant events. Attributes: id, user_id, type, title, body, read_at, created_at.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new manager can create an organization unit and add a subordinate manager within 5 minutes of first access
- **SC-002**: Work item progress is calculable and explainable within 1 second for projects with up to 10,000 work items
- **SC-003**: Users searching for resources receive authorization-filtered results within 2 seconds for up to 100,000 searchable items
- **SC-004**: Every security-sensitive action generates an audit record within 500ms of the action completing
- **SC-005**: Document upload, classification, and indexing completes asynchronously within 60 seconds for documents up to 50 MB
- **SC-006**: 95% of API requests complete within 3 seconds for standard read and write operations
- **SC-007**: A project owner can configure custom metrics and see their impact on work item progress within 10 seconds
- **SC-008**: LLM-processed documents show full provenance (source, model, prompt, timestamp, approval status) to any authorized reviewer within 2 seconds

## Assumptions

- The platform serves organizational users rather than consumers; all users are authenticated through oroidentityserver using Authorization Code with PKCE
- The organization hierarchy depth is unlimited but practical use cases do not exceed 10 levels
- Documents up to 50 MB are supported in v1; larger documents are out of scope; binary content stored in object storage (Blob/MinIO)
- LLM provider selection is configurable but a default provider (e.g., OpenAI, Azure OpenAI) is assumed for initial deployment
- The system operates in a single-tenant model per organization; multi-tenant SaaS is out of scope for v1
- Search uses vector embedding search (pgvector/SQL Server vector) for semantic document content; full-text search is also supported for structured fields
- Background processing uses a queue system appropriate to the Aspire orchestration environment
- Audit records are retained indefinitely or per the organization's retention policy
- The platform is designed for web browser access as the primary client; mobile or desktop clients are out of scope for v1
- Organization hierarchy changes (adding/removing managers) are administrative actions approved by the organization owner
