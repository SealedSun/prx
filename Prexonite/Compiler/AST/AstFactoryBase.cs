using System.Collections.Generic;
using Prexonite.Modular;
using Prexonite.Types;

namespace Prexonite.Compiler.Ast
{
    internal abstract class AstFactoryBase : IAstFactory
    {
        // TODO: (Ticket #106) TryUseSymbolEntry and NullNode should not be defined on AstFactoryBase
        protected abstract AstBlock CurrentBlock { get; }
        protected abstract bool TryUseSymbolEntry(string symbolicId, out SymbolEntry entry);
        protected abstract AstGetSet CreateNullNode(ISourcePosition position);

        public AstTypeExpr ConstantTypeExpression(ISourcePosition position, string typeExpression)
        {
            return new AstConstantTypeExpression(position.File, position.Line, position.Column, typeExpression);
        }

        public AstTypeExpr DynamicTypeExpression(ISourcePosition position, string typeId, IEnumerable<AstExpr> arguments)
        {
            var t = new AstDynamicTypeExpression(position.File, position.Line,position.Column, typeId);
            t.Arguments.AddRange(arguments);
            return t;
        }

        public AstExpr BinaryOperation(ISourcePosition position, AstExpr left, BinaryOperator op, AstExpr right)
        {
            var id = OperatorNames.Prexonite.GetName(op);
            SymbolEntry entry;
            if(TryUseSymbolEntry(id, out entry))
            {
                return new AstBinaryOperator(position.File, position.Line, position.Column, left, op, right, entry,
                                             CurrentBlock);
            }
            else
            {
                return CreateNullNode(position);
            }
        }

        public AstExpr UnaryOperation(ISourcePosition position, UnaryOperator op, AstExpr operand)
        {
            var id = OperatorNames.Prexonite.GetName(op);
            SymbolEntry entry;
            if(TryUseSymbolEntry(id, out entry))
            {
                return new AstUnaryOperator(position.File,position.Line, position.Column, op,operand,entry);
            }
            else
            {
                return CreateNullNode(position);
            }
        }

        public AstExpr Coalescence(ISourcePosition position, IEnumerable<AstExpr> operands)
        {
            var c = new AstCoalescence(position.File,position.Line, position.Column);
            c.Expressions.AddRange(operands);
            return c;
        }

        public AstExpr ConditionalExpression(ISourcePosition position, AstExpr condition, AstExpr thenExpr, AstExpr elseExpr, bool isNegative = false)
        {
            var c = new AstConditionalExpression(position.File, position.Line, position.Column, condition, isNegative)
                {IfExpression = thenExpr, ElseExpression = elseExpr};
            return c;
        }

        public AstExpr Constant(ISourcePosition position, object constant)
        {
            return new AstConstant(position.File,position.Line,position.Column,constant);
        }

        public AstExpr CreateClosure(ISourcePosition position, EntityRef.Function function)
        {
            return new AstCreateClosure(position.File, position.Line, position.Column,function.ToSymbolEntry());
        }

        public AstCreateCoroutine CreateCoroutine(ISourcePosition position, AstExpr function)
        {
            return new AstCreateCoroutine(position.File, position.Line, position.Column) {Expression = function};
        }

        public AstExpr KeyValuePair(ISourcePosition position, AstExpr key, AstExpr value)
        {
            return new AstKeyValuePair(position.File, position.Line, position.Column,key,value);
        }

        public AstExpr ListLiteral(ISourcePosition position, IEnumerable<AstExpr> elements)
        {
            var l = new AstListLiteral(position.File, position.Line, position.Column);
            l.Elements.AddRange(elements);
            return l;
        }

        public AstExpr HashLiteral(ISourcePosition position, IEnumerable<AstExpr> elements)
        {
            var l = new AstHashLiteral(position.File, position.Line, position.Column);
            l.Elements.AddRange(elements);
            return l;
        }

        public AstExpr LogicalAnd(ISourcePosition position, IEnumerable<AstExpr> clauses)
        {
            using (var e = clauses.GetEnumerator())
            {
                if(!e.MoveNext())
                    _throwLogicalNeedsTwoArgs(position);
                var lhs = e.Current;

                if (!e.MoveNext())
                    _throwLogicalNeedsTwoArgs(position);
                var rhs = e.Current;

                var a = new AstLogicalAnd(position.File, position.Line, position.Column, lhs, rhs);

                while (e.MoveNext())
                    a.Conditions.AddLast(e.Current);

                return a;
            }
        }

        public AstExpr LogicalOr(ISourcePosition position, IEnumerable<AstExpr> clauses)
        {
            using (var e = clauses.GetEnumerator())
            {
                if (!e.MoveNext())
                    _throwLogicalNeedsTwoArgs(position);
                var lhs = e.Current;

                if (!e.MoveNext())
                    _throwLogicalNeedsTwoArgs(position);
                var rhs = e.Current;

                var a = new AstLogicalOr(position.File, position.Line, position.Column, lhs, rhs);

                while (e.MoveNext())
                    a.Conditions.AddLast(e.Current);

                return a;
            }
        }

        public AstExpr Null(ISourcePosition position)
        {
            return new AstNull(position.File, position.Line, position.Column);
        }

        public AstExpr ObjectCreation(ISourcePosition position, AstTypeExpr type)
        {
            return new AstObjectCreation(position.File, position.Line, position.Column, type);
        }

        public AstExpr Typecheck(ISourcePosition position, AstExpr operand, AstTypeExpr type)
        {
            return new AstTypecheck(position.File, position.Line, position.Column,operand,type);
        }

        public AstExpr Typecast(ISourcePosition position, AstExpr operand, AstTypeExpr type)
        {
            return new AstTypecast(position.File, position.Line, position.Column,operand,type);
        }

        public AstGetSet Entity(ISourcePosition position, EntityRef entity, PCall call = PCall.Get)
        {
            return AstGetSetEntity.Create(position, call, entity);
        }

        public AstGetSet MemberAccess(ISourcePosition position, AstExpr receiver, string memberId, PCall call = PCall.Get)
        {
            return new AstGetSetMemberAccess(position.File, position.Line, position.Column, call, receiver, memberId);
        }

        public AstGetSet StaticMemberAccess(ISourcePosition position, AstTypeExpr typeExpr, string memberId, PCall call = PCall.Get)
        {
            return new AstGetSetStatic(position.File, position.Line, position.Column,call,typeExpr,memberId);
        }

        public AstGetSet IndirectCall(ISourcePosition position, AstExpr receiver, PCall call = PCall.Get)
        {
            return new AstIndirectCall(position,call,receiver);
        }

        public AstGetSet Placeholder(ISourcePosition position, int? index = new int?())
        {
            return new AstPlaceholder(position.File, position.Line, position.Column, index);
        }

        public AstSubBlock Block(ISourcePosition position)
        {
            return new AstSubBlock(position,CurrentBlock);
        }

        public AstCondition Condition(ISourcePosition position, AstExpr condition, bool isNegative = false)
        {
            return new AstCondition(position, CurrentBlock, condition, isNegative);
        }

        public AstLoop WhileLoop(ISourcePosition position, bool isPostcondition = false, bool isNegative = false)
        {
            return new AstWhileLoop(position,CurrentBlock, isPostcondition,!isNegative);
        }

        public AstForLoop ForLoop(ISourcePosition position)
        {
            return new AstForLoop(position,CurrentBlock);
        }

        public AstForeachLoop ForeachLoop(ISourcePosition position)
        {
            return new AstForeachLoop(position, CurrentBlock);
        }

        public AstNode Return(ISourcePosition position, ReturnVariant returnVariant = ReturnVariant.Exit, AstExpr expression = null)
        {
            return new AstReturn(position.File, position.Line, position.Column, returnVariant) {Expression = expression};
        }

        public AstNode Throw(ISourcePosition position, AstExpr exceptionExpression)
        {
            return new AstThrow(position.File, position.Line, position.Column){Expression = exceptionExpression};
        }

        public AstTryCatchFinally TryCatchFinally(ISourcePosition position)
        {
            return new AstTryCatchFinally(position, CurrentBlock);
        }

        public AstUsing Using(ISourcePosition position)
        {
            return new AstUsing(position,CurrentBlock);
        }

        private void _throwLogicalNeedsTwoArgs(ISourcePosition position)
        {
            throw new PrexoniteException(string.Format("Lazy logical operators require at least two operands. {0}", position));
        }
    }
}