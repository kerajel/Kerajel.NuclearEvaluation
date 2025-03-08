# Nuclear Evaluation

Welcome to the Nuclear Evaluation repository! This project showcases the capabilities of Blazor Server, focusing on dynamic filtering and sorting through the use of data grids and a powerful query builder. These features collectively enhance the interactivity and flexibility of data management within the application. The component library used in this project is Radzen, which provides a rich set of UI components for building Blazor applications.

The application can be accessed via [https://nuclearevaluation.com/](https://nuclearevaluation.com/).

## Features

- **Dynamic Filtering:** Users can operate on data grids, specifying filtering on multiple columns based on various criteria.
- **Dynamic Sorting:** Enables users to sort data by any column, enhancing the flexibility and usability of data presentation.
- **Query Builder:** A query builder that lets users create and modify queries without writing SQL code, simplifying the process of applying complex filters and sorts directly through the user interface.
- **Rapid Tabular Data Upload & STEM Preview:** Utilizes a Rust-powered native library for quick reading of tabular data files (XLS, XLSX, etc.), provided by the [Kerajel.TabularDataReader.Native](https://github.com/kerajel/TabularDataReader.Native) with robust result handling via [Kerajel.Primitives](https://github.com/kerajel/Primitives). This integration offers a seamless preview experience, enabling users to quickly validate and interact with their data before further analysis.
- **Bulk Operations and Temp Table Support:** Employs Linq2Db for efficient management of scenarios requiring temporary tables and bulk operations.

## License
MIT Licensed.
