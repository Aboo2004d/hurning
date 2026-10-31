# Category inference and main fields

The source extraction pipeline now classifies categories against `CategoryTaxonomy`.
There are 13 proposed main fields, with specific canonical specialties below each.
Known Arabic/English synonyms resolve to one Arabic label. Exact known aliases are
used; spelling similarity alone does not create a category or an alert equivalence.

A missing or unknown AI category can be recovered from a clear role title. A generic
job title can use distinctive phrases in its explicitly headed responsibilities
section for a single-role announcement. Unrelated skills or the employer's industry
are insufficient. The global source body is not used for multiple roles. Ambiguity
leaves the category empty with a review reason, rather than publishing an invented
classification. Canonical categories and administrator-approved custom categories
remain supported. The original AI JSON remains unchanged.

For semantic inference across the full announcement, the AI prompt policy tells the
model to classify the actual work, training subject or tender goods/services using
the controlled catalog. It does not require a verbatim field label in the source.
Saved historical prompts are immutable: deploying code does not rewrite an active
or pending version. Open `/ar/Admin/CategoryGroups`, expand the main-field proposals,
then use the category-policy link to open a draft based on the pending/active prompt.
Review and save it through the existing versioned prompt editor to queue activation.
The current code default also includes this policy; existing migration seed text is
unchanged.

The same page previews each main field and its specialties. Review a field proposal
and save it to approve a group using the existing group editor. A proposal includes
known existing aliases and canonical choices but does not steal labels already
assigned to another group. Opening a proposal does not save any changes. Existing
opportunity labels and member preferences are not bulk rewritten.

**Alert behavior:** the existing approved-group system treats all members of a group
as equivalent and counts them as one preference. Grouping civil and electrical
engineering under engineering therefore broadens alerts to both specialties. This
is a two-level classification proposal using existing groups, not an independent
nested subscription hierarchy. Keep specialties in separate groups when narrow alert
matching is required. The administration page states this before group approval.

Old blank categories are corrected when their source items are reprocessed (subject
to the existing prompt-version/manual-override protections). Historical duplicate
labels can be grouped without changing existing records. No migration is required.
No production data or external AI requests were used for verification.

Checks:

- `--category-classification-check`: canonical aliases, roles, responsibilities,
  ambiguous roles, training subjects, specialties and parent proposals.
- `--extraction-completeness-check`: isolated SQLite extraction persistence, raw
  JSON retention, source-role identity, family proposals and prompt drafts.
- `--category-group-check`, `--category-preference-check`, `--workflow-check`.
