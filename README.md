RQ Data Integration App
=======================

| [/api/Data-Connect/][1] | [/guides/Data-Connect][2] | [/api/customized-reports][3] |
| ----------------------- | ------------------------- | ---------------------------- |

## Parameters

1. ObjectToRun
   * **All**: Run all the below pulls.
   * **Locations**: Get all the locations.
   * **Customers**: Get all the customers who have sales from yesterday.
   * **Employees**: Get all the employees.
   * **Sales**: Get all sales from yesterday.
   * **PaymentIntegrationTransaction**: Get all PaymentIntegrationTransaction records from yesterday.
   * **Logging**: Get all API requests made from yesterday to now.

## Functionality

1. Get the requested data.
2. Insert data into staging table in the TARDIS DB.
3. Create an archive file on SQL server at Archive\Archive\{name of data source}. These file are all prfixed with "api_".
4. Move to next division/integration if any.

## App location

The app is run from the SQL server at Archive\ImportApps\RQDataIntegration

[1]: https://developers.iqmetrix.com/api/RQ-Data-Connect/
[2]: https://developers.iqmetrix.com/guides/Data-Connect/
[3]: https://developers.iqmetrix.com/api/customized-reports/