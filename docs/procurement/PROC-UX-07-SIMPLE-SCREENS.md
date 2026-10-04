# PROC-UX-07 — Simple Procurement screens

**Reference:** Screenshots supplied on 4 October 2026 in `/home/amizo/random uploads`. They show the layout and interaction patterns the business wants for Procurement. Use them as a design reference, not as a specification for Zoho behavior or a requirement to copy its branding.

## What the screenshots make clear

- A record opens with its **name or number, status, key amount or date, and one clear next action**.
- Common actions sit near the top. Long histories, linked transactions, and specialist checks are available through tabs or expandable sections.
- Creation forms ask for the essentials first. Optional details are grouped; a person can see their progress and save work before submitting.
- List pages make **Add**, **Import**, search, and filters easy to find. An empty list says what the record is for and offers a working action.
- Vendor, item, request, approval, delivery, budget, and report pages follow the same visual rhythm even though their fields differ.
- A person should be able to answer **What is this? What is its status? What do I do next?** without reading a policy document.

## Apply this pattern in our ERP

1. Keep the existing side menu structure. Give each page a short title, a one-sentence explanation where useful, and a single main action.
2. Show four or fewer key facts above the fold on detail pages. Put the remaining fields in **Details**.
3. Use **Items**, **Details**, and **History** tabs for purchase requests. Show each requested item as a readable card with quantity, approval, order progress, and its available buying action. Expand for delivery breakdown and linked records.
4. Show a vendor's main contact, open orders/bills, and next step before internal approval evidence. Keep checks and documents reachable together.
5. Keep buying controls accurate: an unavailable action must say why, and no visual simplification may bypass approval, budget, supplier, or Finance controls.
6. Use the wording in [PROC-UX-06](PROC-UX-06-PLAIN-LANGUAGE.md) for headings, actions, status labels, and empty states.
7. Make reports browseable by name and searchable. Show the result first; put calculation rules behind **How this report is calculated**.
8. When a flow says a draft is saved, save the entered values as well as the current step. Keep the final action unavailable until the required fields are complete, and show what is missing.

## Review checklist

- Can an employee create a request from the first screen without understanding procurement jargon?
- Can a manager find the approval decision and the budget result in one glance?
- Can a buyer choose the next buying step for each approved item without scanning a wide table?
- Can a vendor record be understood from its Overview before opening internal checks?
- At narrow widths, do actions and facts remain readable without a wide horizontal table?
- Do all forms, tabs, links, and empty-state actions work with keyboard and pointer input?
