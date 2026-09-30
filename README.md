# Componyx.Data

A lightweight object-relational mapper for .NET, built directly on ADO.NET. Write a query, in T-SQL directly or through its fluent syntax builder, and get back fully mapped objects, relations included, without writing joins or mapping code by hand.

It has been in continuous production use for over a decade, handling everything from simple CRUD to deep object graphs spanning multiple databases.

## Built on ADO.NET

ADO.NET is the most fundamental data access layer .NET has offered since the beginning, the connections, commands and readers that every higher-level data library, including Entity Framework, is ultimately built on top of. Componyx.Data stays close to that foundation instead of adding another abstraction layer on top of one.

## Features

- **Predictable SQL.** Query methods map directly to SQL clauses, no LINQ expression tree being translated by a query planner that could guess wrong. Relation joins and paging queries are generated using fixed rules based on your database's dialect, including correctly limiting by distinct object rather than by joined row when a query returns one-to-many relations, so a page of 10 is always 10 objects.
- **Automatic relation loading.** One-to-many and one-to-one relations load as part of the same query, with proper joins, no explicit chain of includes required.
- **True multi-database support.** SQL Server, MySQL, PostgreSQL, Oracle and SQLite, through explicit dialect handling for paging, result limiting and identifier escaping.
- **Cross-database queries.** Individual classes can map to a different catalog, schema or server than your default connection, and relation joins across that boundary still work.
- **Database-first.** Reads your existing schema rather than generating or altering it. If you already own your schema, there's no migrations layer standing between you and it.
- **No framework dependency.** No reliance on Entity Framework or any external data-access stack.
- **Full async support.** Every operation has a matching async method, built on real async ADO.NET calls.

## Scope

Componyx.Data has no migrations engine, no code-first schema generation, and no LINQ provider for translating arbitrary expressions into SQL. If your project needs either of those, this isn't the right fit.

## Documentation

Full documentation and examples: [componyx.com](https://componyx.com/orm)

## Requirements

.NET 10 or later.

## License

Componyx.Common is licensed under the [MIT License](LICENSE.md).