Initial prompt
==============

Create a mock server that can simulate three servers for the three use cases: OpsAPI, Salesforce, and NetworkRailAPI.

This demo is related to the train company Eurostar but uses the fictional company name "Starline".

The mock server must include either APIs and web admin console to:

- Generate fake data on the fly (with a "Generate new data" button).
- Display data in a table. Tables must be filterable, sortable, and each item must be viewable in a floating popup.

By default, generate an initial realistic dataset. All data must be kept in memory; do not use a database.

Create mockup data based on information captured in the usecase file : documents\Azure Integration Services Use Cases.md

The global project name is Transgrid.

If needed, add a hamburger menu with settings.

The site should offer a modern user interface with a polished, professional look and feel. If there are tables, they must support sorting, filtering, and include a quick search bar.

- Modern user interface — attractive design with gradients, animations, and visual effects
- Real-time filtering — search bar for instant filtering
- Click column headers to sort (ascending/descending)

Add a button to delete all in-memory data and reset to a baseline dataset.

Target platform and technologies:

- .NET 10
- ASP.NET (web framework)
- Razor Pages — user interface
- Web API — REST API for CRUD operations
- Bootstrap 5 — responsive design
- Bootstrap Icons

Store the source code in a sources/server folder.
