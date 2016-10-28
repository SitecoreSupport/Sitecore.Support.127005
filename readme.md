# Sitecore.Support.127005
When submitting a form, a visitor sees the Success Message even if saving form data to SQL database failed.

## Description
This patch enables showing error message in the form when saving form data to the database failed.
Note: the patch assumes that the "sqlFormsDataProvider" is used and all CM and CD servers have a valid connection string to the WFFM SQL Server database specified.

## License  
This patch is licensed under the [Sitecore Corporation A/S License for GitHub](https://github.com/sitecoresupport/Sitecore.Support.127005/blob/master/LICENSE).  

## Download  
Downloads are available via [GitHub Releases](https://github.com/sitecoresupport/Sitecore.Support.127005/releases).  