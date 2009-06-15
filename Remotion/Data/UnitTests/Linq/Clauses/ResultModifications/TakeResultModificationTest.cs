// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// version 3.0 as published by the Free Software Foundation.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Remotion.Data.Linq.Clauses.ResultModifications;

namespace Remotion.Data.UnitTests.Linq.Clauses.ResultModifications
{
  [TestFixture]
  public class TakeResultModificationTest
  {
    private TakeResultModification _resultModification;

    [SetUp]
    public void SetUp ()
    {
      _resultModification = new TakeResultModification (ExpressionHelper.CreateSelectClause (), 2);
    }

    [Test]
    public void Clone ()
    {
      var newSelectClause = ExpressionHelper.CreateSelectClause ();
      var clone = _resultModification.Clone (newSelectClause);

      Assert.That (clone, Is.InstanceOfType (typeof (TakeResultModification)));
      Assert.That (clone.SelectClause, Is.SameAs (newSelectClause));
      Assert.That (((TakeResultModification) clone).Count, Is.EqualTo (2));
    }

    [Test]
    public void ExecuteInMemory ()
    {
      var items = new[] { 1, 2, 3, 0, 2 };
      var result = _resultModification.ExecuteInMemory (items);

      Assert.That (result.Cast<int>().ToArray(), Is.EqualTo (new[] { 1, 2 }));
    }

    [Test]
    public void ConvertStreamToResult ()
    {
      var items = new[] { 1, 2, 3, 0, 2 };
      var result = _resultModification.ConvertStreamToResult (items);

      Assert.That (result, Is.SameAs (items));
    }
  }
}