// This file is part of the re-linq project (relinq.codeplex.com)
// Copyright (c) rubicon IT GmbH, www.rubicon.eu
// 
// re-linq is free software; you can redistribute it and/or modify it under 
// the terms of the GNU Lesser General Public License as published by the 
// Free Software Foundation; either version 2.1 of the License, 
// or (at your option) any later version.
// 
// re-linq is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-linq; if not, see http://www.gnu.org/licenses.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;
using Remotion.Linq.Development.UnitTesting;
using Remotion.Linq.UnitTests.TestDomain;

namespace Remotion.Linq.UnitTests.Clauses.ResultOperators
{
  [TestFixture]
  public class UnionResultOperatorTest
  {
    private UnionResultOperator _resultOperator;
    private Expression _source2;

    [SetUp]
    public void SetUp ()
    {
      _source2 = Expression.Constant (new[] { 2 });
      _resultOperator = new UnionResultOperator ("itemName", typeof (int), _source2);
    }

    [Test]
    public void Initialization ()
    {
      Assert.That (_resultOperator.ItemName, Is.EqualTo ("itemName"));
      Assert.That (_resultOperator.Source2, Is.EqualTo (_source2));
      Assert.That (_resultOperator.ItemType, Is.EqualTo (typeof (int)));
    }

    [Test]
    public void GetConstantSource2 ()
    {
      Assert.That (_resultOperator.GetConstantSource2 (), Is.SameAs (((ConstantExpression) _source2).Value));
    }

    [Test]
    [ExpectedException (typeof (InvalidOperationException))]
    public void GetConstantSource2_NoConstantExpression ()
    {
      var resultOperator = new UnionResultOperator ("i", typeof (string), Expression.Parameter (typeof (IEnumerable<string>), "ss"));
      resultOperator.GetConstantSource2 ();
    }

    [Test]
    public void Clone ()
    {
      var clonedClauseMapping = new QuerySourceMapping ();
      var cloneContext = new CloneContext (clonedClauseMapping);
      var clone = _resultOperator.Clone (cloneContext);

      Assert.That (clone, Is.InstanceOf (typeof (UnionResultOperator)));
      Assert.That (((UnionResultOperator) clone).ItemName, Is.EqualTo ("itemName"));
      Assert.That (((UnionResultOperator) clone).Source2, Is.SameAs (_source2));
      Assert.That (((UnionResultOperator) clone).ItemType, Is.SameAs (typeof (int)));
    }

    [Test]
    public void ExecuteInMemory ()
    {
      var items = new[] { 1, 2, 3 };
      var input = new StreamedSequence (items, new StreamedSequenceInfo (typeof (int[]), Expression.Constant (0)));
      var result = _resultOperator.ExecuteInMemory<int> (input);

      Assert.That (result.GetTypedSequence<int>().ToArray(), Is.EquivalentTo (new[] { 1, 2, 3 }));
    }

    [Test]
    public void GetOutputDataInfo ()
    {
      var intExpression = Expression.Constant (0);
      var input = new StreamedSequenceInfo (typeof (int[]), intExpression);
      var result = _resultOperator.GetOutputDataInfo (input);

      Assert.That (result, Is.InstanceOf (typeof (StreamedSequenceInfo)));
      Assert.That (result.DataType, Is.SameAs (typeof (IQueryable<int>)));
      Assert.That (((StreamedSequenceInfo) result).ItemExpression, Is.InstanceOf (typeof (QuerySourceReferenceExpression)));
      Assert.That (
          ((QuerySourceReferenceExpression) ((StreamedSequenceInfo) result).ItemExpression).ReferencedQuerySource,
          Is.SameAs (_resultOperator));
    }

    [Test]
    public void GetOutputDataInfo_AssignableSource2 ()
    {
      var resultOperator = new UnionResultOperator ("i", typeof (object), Expression.Constant (new[] { "string" }));

      var cookExpression = Expression.Constant (null, typeof (Cook));
      var input = new StreamedSequenceInfo (typeof (Cook[]), cookExpression);

      var result = resultOperator.GetOutputDataInfo (input);

      Assert.That (result, Is.InstanceOf (typeof (StreamedSequenceInfo)));
      Assert.That (result.DataType, Is.SameAs (typeof (IQueryable<object>)));
    }

    [Test]
    [ExpectedException (typeof (ArgumentException), ExpectedMessage = 
        "Parameter 'inputInfo' has type 'Remotion.Linq.Clauses.StreamedData.StreamedScalarValueInfo' "
        + "when type 'Remotion.Linq.Clauses.StreamedData.StreamedSequenceInfo' was expected."
        + "\r\nParameter name: inputInfo")]
    public void GetOutputDataInfo_InvalidInputType ()
    {
      var input = new StreamedScalarValueInfo (typeof (int));
      _resultOperator.GetOutputDataInfo (input);
    }

    [Test]
    [ExpectedException (typeof (ArgumentException), ExpectedMessage = 
        "The input sequence must have items of type 'System.Int32', but it has items of type 'System.String'.\r\nParameter name: inputInfo")]
    public void GetOutputDataInfo_InvalidInputItemType ()
    {
      var input = new StreamedSequenceInfo (typeof (string[]), Expression.Constant (""));
      _resultOperator.GetOutputDataInfo (input);
    }

    [Test]
    public void TransformExpressions ()
    {
      var oldExpression = ExpressionHelper.CreateExpression ();
      var newExpression = ExpressionHelper.CreateExpression ();
      var resultOperator = new UnionResultOperator ("i", typeof (int), oldExpression);

      resultOperator.TransformExpressions (ex =>
      {
        Assert.That (ex, Is.SameAs (oldExpression));
        return newExpression;
      });

      Assert.That (resultOperator.Source2, Is.SameAs (newExpression));
    }
  }
}