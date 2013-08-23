﻿#region Copyright, Author Details and Related Context
//<notice lastUpdateOn="8/23/2013">
//  <assembly>PodFetch</assembly>
//  <description>A simple "Astronomy Picture Of The Day" (APOD) downloader that leverages TPL Dataflow</description>
//  <copyright>
//    Copyright (C) 2013 Louis S. Berman

//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.

//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.

//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/.
//  </copyright>
//  <author>
//    <fullName>Louis S. Berman</fullName>
//    <email>louis@squideyes.com</email>
//    <website>http://squideyes.com</website>
//  </author>
//</notice>
#endregion 

namespace PodFetch
{
    public enum Status
    {
        Info,
        Scraping,
        Scraped,
        Fetching,
        Fetched,
        DupImage,
        BadScrape,
        BadFetch,
        BadGetUrls,
        NoImage,
        Cancelled,
        Failure,
        Finished
    }
}
