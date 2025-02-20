﻿namespace Interpreter
{
	using PrefixParseFn = Func<Expression?>;
	using InfixParseFn = Func<Expression, Expression?>;

	public class Parser
	{
		public Lexer Lexer { get; set; }
		private Token CurrentToken { get; set; }
		private Token PeekToken { get; set; }
		public List<string> Errors { get; set; }

		public Dictionary<TokenType, Delegate> PrefixParseFunctions { get; set; }
		public Dictionary<TokenType, Delegate> InfixParseFunctions { get; set; }

		public Dictionary<TokenType, Precedence> Precedences { get; set; }

		public Parser(Lexer lexer)
		{
			Lexer = lexer;
			Errors = new List<string>();

			PrefixParseFunctions = new Dictionary<TokenType, Delegate>();
			InfixParseFunctions = new Dictionary<TokenType, Delegate>();

			Precedences = new Dictionary<TokenType, Precedence>
			{
				{TokenType.EQ, Precedence.EQUALS},
				{TokenType.NEQ, Precedence.EQUALS},
				{TokenType.LT, Precedence.LESSGREATER},
				{TokenType.GT, Precedence.LESSGREATER},
				{TokenType.PLUS, Precedence.SUM},
				{TokenType.MINUS, Precedence.SUM},
				{TokenType.SLASH, Precedence.PRODUCT},
				{TokenType.ASTERIX, Precedence.PRODUCT},
				{TokenType.LPAREN, Precedence.CALL},
				{TokenType.LBRACKET, Precedence.INDEX},
			};

			RegisterParseFunctions();

			AdvanceTokens();
		}

		private void RegisterParseFunctions()
		{
			RegisterPrefix(TokenType.IDENT, ParseIdentifier);
			RegisterPrefix(TokenType.INT, ParseIntegerLiteral);
			RegisterPrefix(TokenType.STRING, ParseStringLiteral);
			RegisterPrefix(TokenType.TRUE, ParseBoolean);
			RegisterPrefix(TokenType.FALSE, ParseBoolean);
			RegisterPrefix(TokenType.BANG, ParsePrefixExpression);
			RegisterPrefix(TokenType.MINUS, ParsePrefixExpression);
			RegisterPrefix(TokenType.LPAREN, ParseGroupedExpression);
			RegisterPrefix(TokenType.IF, ParseIfExpression);
			RegisterPrefix(TokenType.FUNCTION, ParseFunctionLiteral);
			RegisterPrefix(TokenType.LBRACKET, ParseArrayLiteral);

			RegisterInfix(TokenType.LBRACKET, ParseIndexExpression);
			RegisterInfix(TokenType.LPAREN, ParseCallExpression);
			RegisterInfix(TokenType.PLUS, ParseInfixExpression);
			RegisterInfix(TokenType.MINUS, ParseInfixExpression);
			RegisterInfix(TokenType.SLASH, ParseInfixExpression);
			RegisterInfix(TokenType.ASTERIX, ParseInfixExpression);
			RegisterInfix(TokenType.EQ, ParseInfixExpression);
			RegisterInfix(TokenType.NEQ, ParseInfixExpression);
			RegisterInfix(TokenType.LT, ParseInfixExpression);
			RegisterInfix(TokenType.GT, ParseInfixExpression);
		}

		public static void PrintProgram(Program? program)
		{
			Console.WriteLine(program?.String(0, program.Statements.Count > 1));
		}

		private void PeekError(TokenType tokenType)
		{
			Errors.Add($"Expected next token to be {tokenType}, got {PeekToken.Type} instead.");
		}

		private void IntegerParseError(string integer)
		{
			Errors.Add($"Could not parse {integer} as integer.");
		}

		private void NoPrefixParseFunctionError(TokenType tokenType)
		{
			Errors.Add($"No prefix parse function for {tokenType}.");
		}

		private void RegisterPrefix(TokenType tokenType, PrefixParseFn prefixParseFunction)
		{
			PrefixParseFunctions[tokenType] = prefixParseFunction;
		}

		private void RegisterInfix(TokenType tokenType, InfixParseFn infixParseFunction)
		{
			InfixParseFunctions[tokenType] = infixParseFunction;
		}

		private void AdvanceTokens()
		{
			CurrentToken = PeekToken;
			PeekToken = Lexer.NextToken();
		}

		public Program ParseProgram()
		{
			Program program = new Program();
			AdvanceTokens();

			while (CurrentToken.Type != TokenType.EOF)
			{
				Statement? statement = ParseStatement();
				if (statement != null)
					program.Statements.Add(statement);
				AdvanceTokens();
			}
			return program;
		}

		private Statement? ParseStatement()
		{
			return CurrentToken.Type switch
			{
				TokenType.LET => ParseLetStatement(),
				TokenType.RETURN => ParseReturnStatement(),
				_ => ParseExpressionStatement()
			};
		}

		private LetStatement? ParseLetStatement()
		{
			LetStatement letStatement = new() { Token = CurrentToken };

			if (ExpectPeek(TokenType.IDENT) == false)
				return null;

			letStatement.Name = new Identifier() { Token = CurrentToken, Value = CurrentToken.Literal };

			if (ExpectPeek(TokenType.ASSIGN) == false)
				return null;

			AdvanceTokens();
			letStatement.Value = ParseExpression(Precedence.LOWEST);

			if (PeekToken.Type == TokenType.SEMICOLON)
				AdvanceTokens();

			return letStatement;
		}

		private ReturnStatement ParseReturnStatement()
		{
			ReturnStatement returnStatement = new ReturnStatement { Token = CurrentToken };

			AdvanceTokens();
			returnStatement.ReturnValue = ParseExpression(Precedence.LOWEST);

			if (PeekToken.Type == TokenType.SEMICOLON)
				AdvanceTokens();

			return returnStatement;
		}

		private ExpressionStatement ParseExpressionStatement()
		{
			ExpressionStatement expressionStatement = new ExpressionStatement
			{
				Token = CurrentToken,
				Expression = ParseExpression(Precedence.LOWEST)
			};

			if (PeekToken.Type == TokenType.SEMICOLON)
				AdvanceTokens();

			return expressionStatement;
		}

		private Expression? ParseExpression(Precedence precedence)
		{
			var ok = PrefixParseFunctions.TryGetValue(CurrentToken.Type, out var parsePrefix);
			if (!ok)
			{
				NoPrefixParseFunctionError(CurrentToken.Type);
				return null;
			}
			Expression? leftExpression = (Expression?)parsePrefix?.DynamicInvoke();
			if (leftExpression == null)
				return null;

			while (PeekToken.Type != TokenType.SEMICOLON && precedence < PeekPrecedence())
			{
				ok = InfixParseFunctions.TryGetValue(PeekToken.Type, out var parseInfix);
				if (!ok)
					return leftExpression;

				AdvanceTokens();
				leftExpression = (Expression?)parseInfix?.DynamicInvoke(leftExpression);
				if (leftExpression == null)
					return null;
			}

			return leftExpression;
		}

		private Identifier ParseIdentifier()
		{
			return new Identifier { Token = CurrentToken, Value = CurrentToken.Literal };
		}

		private IntegerLiteral? ParseIntegerLiteral()
		{
			IntegerLiteral integerLiteral = new IntegerLiteral { Token = CurrentToken };
			bool error = int.TryParse(CurrentToken.Literal, out int value);
			if (error == false)
			{
				IntegerParseError(CurrentToken.Literal);
				return null;
			}

			integerLiteral.Value = value;
			return integerLiteral;
		}

		private StringLiteral ParseStringLiteral()
		{
			return new StringLiteral { Token = CurrentToken, Value = CurrentToken.Literal };
		}

		private BooleanLiteral ParseBoolean()
		{
			return new BooleanLiteral { Token = CurrentToken, Value = CurrentToken.Type == TokenType.TRUE };
		}

		private Expression? ParseGroupedExpression()
		{
			AdvanceTokens();
			Expression? expression = ParseExpression(Precedence.LOWEST);
			return ExpectPeek(TokenType.RPAREN) == false ? null : expression;
		}

		private Expression? ParseIfExpression()
		{
			IfExpression ifExpression = new IfExpression { Token = CurrentToken };

			if (ExpectPeek(TokenType.LPAREN) == false)
				return null;

			AdvanceTokens();
			ifExpression.Condition = ParseExpression(Precedence.LOWEST);

			if (ExpectPeek(TokenType.RPAREN) == false)
				return null;

			if (ExpectPeek(TokenType.LBRACE) == false)
				return null;

			ifExpression.Consequence = ParseBlockStatement();

			if (PeekToken.Type != TokenType.ELSE) return ifExpression;

			AdvanceTokens();
			if (ExpectPeek(TokenType.LBRACE) == false)
				return null;

			ifExpression.Alternative = ParseBlockStatement();

			return ifExpression;
		}

		private Expression? ParseFunctionLiteral()
		{
			FunctionLiteral functionLiteral = new FunctionLiteral { Token = CurrentToken };

			if (ExpectPeek(TokenType.LPAREN) == false)
				return null;

			functionLiteral.Parameters = ParseFunctionParameters();

			if (ExpectPeek(TokenType.LBRACE) == false)
				return null;

			functionLiteral.Body = ParseBlockStatement();

			return functionLiteral;
		}

		private List<Identifier>? ParseFunctionParameters()
		{
			List<Identifier> parameters = new List<Identifier>();

			if (PeekToken.Type == TokenType.RPAREN)
			{
				AdvanceTokens();
				return parameters;
			}
			AdvanceTokens();

			Identifier parameter = new Identifier { Token = CurrentToken, Value = CurrentToken.Literal };
			parameters.Add(parameter);

			while (PeekToken.Type == TokenType.COMMA)
			{
				AdvanceTokens();
				AdvanceTokens();

				parameter = new Identifier { Token = CurrentToken, Value = CurrentToken.Literal };
				parameters.Add(parameter);
			}

			return ExpectPeek(TokenType.RPAREN) == false ? null : parameters;
		}

		private Expression ParseArrayLiteral()
		{
			return new ArrayLiteral { Token = CurrentToken, Elements = ParseExpressionList(TokenType.RBRACKET) };
		}

		private List<Expression?>? ParseExpressionList(TokenType end)
		{
			List<Expression?> list = new();

			if (PeekToken.Type == end)
			{
				AdvanceTokens();
				return list;
			}

			AdvanceTokens();
			list.Add(ParseExpression(Precedence.LOWEST));

			while (PeekToken.Type == TokenType.COMMA)
			{
				AdvanceTokens();
				AdvanceTokens();
				list.Add(ParseExpression(Precedence.LOWEST));
			}

			return ExpectPeek(end) == false ? null : list;
		}

		private Expression? ParseIndexExpression(Expression left)
		{
			IndexExpression indexExpression = new IndexExpression { Token = CurrentToken, Left = left };
			AdvanceTokens();
			indexExpression.Index = ParseExpression(Precedence.LOWEST);
			return ExpectPeek(TokenType.RBRACKET) == false ? null : indexExpression;
		}

		private Expression ParseCallExpression(Expression function)
		{
			return new CallExpression { Token = CurrentToken, Function = function, Arguments = ParseExpressionList(TokenType.RPAREN) };
		}

		private BlockStatement ParseBlockStatement()
		{
			BlockStatement blockStatement = new BlockStatement { Token = CurrentToken, Statements = new List<Statement?>() };

			AdvanceTokens();

			while (CurrentToken.Type != TokenType.RBRACE && CurrentToken.Type != TokenType.EOF)
			{
				Statement? statement = ParseStatement();
				if (statement != null)
					blockStatement.Statements.Add(statement);
				AdvanceTokens();
			}

			return blockStatement;
		}

		private PrefixExpression ParsePrefixExpression()
		{
			PrefixExpression prefixExpression = new PrefixExpression
				{Token = CurrentToken, Operator = CurrentToken.Literal};

			AdvanceTokens();
			prefixExpression.RightExpression = ParseExpression(Precedence.PREFIX);

			return prefixExpression;
		}

		private InfixExpression ParseInfixExpression(Expression leftExpression)
		{
			InfixExpression infixExpression = new InfixExpression
				{Token = CurrentToken, Operator = CurrentToken.Literal, LeftExpression = leftExpression};

			Precedence precedence = CurrentPrecedence();
			AdvanceTokens();
			infixExpression.RightExpression = ParseExpression(precedence);

			return infixExpression;
		}

		private bool ExpectPeek(TokenType tokenType)
		{
			if (PeekToken.Type == tokenType)
			{
				AdvanceTokens();
				return true;
			}

			PeekError(tokenType);
			return false;
		}

		private Precedence PeekPrecedence()
		{
			return Precedences.ContainsKey(PeekToken.Type) ? Precedences[PeekToken.Type] : Precedence.LOWEST;
		}

		private Precedence CurrentPrecedence()
		{
			return Precedences.ContainsKey(CurrentToken.Type) ? Precedences[CurrentToken.Type] : Precedence.LOWEST;
		}
	}

	public class ParserException : Exception
	{
		public ParserException() {}
		public ParserException(string message) : base(message) {}
		public ParserException(string message, Exception inner) : base(message, inner) {}
	}

	public enum Precedence
	{
		LOWEST,
		EQUALS,
		LESSGREATER,
		SUM,
		PRODUCT,
		PREFIX,
		CALL,
		INDEX
	}
}
