﻿#region Includes



#endregion

#region License

//Microsoft Public License (Ms-PL)

//This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.

//1. Definitions

//The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law.

//A "contribution" is the original software, or any additions or changes to the software.

//A "contributor" is any person that distributes its contribution under this license.

//"Licensed patents" are a contributor's patent claims that read directly on its contribution.

//2. Grant of Rights

//(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.

//(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

//3. Conditions and Limitations

//(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.

//(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.

//(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.

//(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.

//(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.

#endregion

namespace KellermanSoftware.CompareNetObjects
{
    /// <summary>
    ///     Class that allows comparison of two objects of the same type to each other.  Supports classes, lists, arrays,
    ///     dictionaries, child comparison and more.
    /// </summary>
    /// <example>
    ///     CompareLogic compareLogic = new CompareLogic();
    ///     Person person1 = new Person();
    ///     person1.DateCreated = DateTime.Now;
    ///     person1.Name = "Greg";
    ///     Person person2 = new Person();
    ///     person2.Name = "John";
    ///     person2.DateCreated = person1.DateCreated;
    ///     ComparisonResult result = compareLogic.Compare(person1, person2);
    ///     if (!result.AreEqual)
    ///     Console.WriteLine(result.DifferencesString);
    /// </example>
    public class CompareLogic : ICompareLogic
    {
        #region Properties

        /// <summary>
        ///     The default configuration
        /// </summary>
        public ComparisonConfig Config { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        ///     Set up defaults for the comparison
        /// </summary>
        public CompareLogic()
        {
            Config = new ComparisonConfig();
        }

        /// <summary>
        ///     Pass in the configuration
        /// </summary>
        /// <param name="config"></param>
        public CompareLogic(ComparisonConfig config)
        {
            Config = config;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Compare two objects of the same type to each other.
        /// </summary>
        /// <remarks>
        ///     Check the Differences or DifferencesString Properties for the differences.
        ///     Default MaxDifferences is 1 for performance
        /// </remarks>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <returns>True if they are equal</returns>
        public ComparisonResult Compare(object object1, object object2)
        {
            ComparisonResult result = new ComparisonResult(Config);

#if !PORTABLE
            result.Watch.Start();
#endif

            RootComparer rootComparer = RootComparerFactory.GetRootComparer();

            CompareParms parms = new CompareParms
            {
                Config = Config,
                Result = result,
                Object1 = object1,
                Object2 = object2,
                BreadCrumb = string.Empty
            };

            rootComparer.Compare(parms);

            if (Config.AutoClearCache)
                ClearCache();

#if !PORTABLE
            result.Watch.Stop();
#endif

            return result;
        }

        /// <summary>
        ///     Reflection properties and fields are cached. By default this cache is cleared automatically after each compare.
        /// </summary>
        public void ClearCache()
        {
            Cache.ClearCache();
        }

        #endregion
    }
}