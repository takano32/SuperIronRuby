# frozen_string_literal: true
#
# Enumerable implemented in Ruby on top of #each. This is also the interpreter's
# first substantial dogfood: every method here is parsed and run by SuperIronRuby
# itself at engine startup. Blockless (Enumerator) forms raise NotImplementedError.

module Enumerable
  def map
    raise NotImplementedError, "Enumerator not supported yet" unless block_given?
    result = []
    each { |x| result << yield(x) }
    result
  end
  alias collect map

  def flat_map
    result = []
    each { |x| v = yield(x); v.is_a?(Array) ? result.concat(v) : result << v }
    result
  end

  def select
    result = []
    each { |x| result << x if yield(x) }
    result
  end
  alias filter select
  alias find_all select

  def reject
    result = []
    each { |x| result << x unless yield(x) }
    result
  end

  def find
    each { |x| return x if yield(x) }
    nil
  end
  alias detect find

  def each_with_index
    i = 0
    each { |x| yield(x, i); i += 1 }
    self
  end

  def each_with_object(obj)
    each { |x| yield(x, obj) }
    obj
  end

  def reduce(init = nil, sym = nil)
    if sym.nil? && !init.is_a?(Symbol)
      acc = init
      first = init.nil?
      each do |x|
        if first
          acc = x
          first = false
        else
          acc = block_given? ? yield(acc, x) : acc
        end
      end
      acc
    else
      op = sym || init
      acc = sym ? init : nil
      first = acc.nil?
      each do |x|
        if first
          acc = x
          first = false
        else
          acc = acc.__send__(op, x)
        end
      end
      acc
    end
  end
  alias inject reduce

  def sum(init = 0)
    acc = init
    each { |x| acc += block_given? ? yield(x) : x }
    acc
  end

  def count(item = (no_arg = true; nil))
    n = 0
    if !no_arg
      each { |x| n += 1 if x == item }
    elsif block_given?
      each { |x| n += 1 if yield(x) }
    else
      each { |_| n += 1 }
    end
    n
  end

  def include?(item)
    each { |x| return true if x == item }
    false
  end
  alias member? include?

  def to_a
    result = []
    each { |x| result << x }
    result
  end
  alias entries to_a

  def min
    result = nil
    first = true
    each do |x|
      if first
        result = x
        first = false
      elsif (x <=> result) < 0
        result = x
      end
    end
    result
  end

  def max
    result = nil
    first = true
    each do |x|
      if first
        result = x
        first = false
      elsif (x <=> result) > 0
        result = x
      end
    end
    result
  end

  def min_by
    best = nil
    best_key = nil
    first = true
    each do |x|
      k = yield(x)
      if first || (k <=> best_key) < 0
        best = x
        best_key = k
        first = false
      end
    end
    best
  end

  def max_by
    best = nil
    best_key = nil
    first = true
    each do |x|
      k = yield(x)
      if first || (k <=> best_key) > 0
        best = x
        best_key = k
        first = false
      end
    end
    best
  end

  def sort
    to_a.sort
  end

  def sort_by
    # decorate-sort-undecorate; Array#<=> compares [key, value] by key first
    to_a.map { |x| [yield(x), x] }.sort.map { |pair| pair[1] }
  end

  def all?
    each { |x| return false unless (block_given? ? yield(x) : x) }
    true
  end

  def any?
    each { |x| return true if (block_given? ? yield(x) : x) }
    false
  end

  def none?
    each { |x| return false if (block_given? ? yield(x) : x) }
    true
  end

  def first(n = nil)
    if n.nil?
      each { |x| return x }
      nil
    else
      result = []
      each { |x| break if result.length >= n; result << x }
      result
    end
  end

  def take(n)
    result = []
    each { |x| break if result.length >= n; result << x }
    result
  end

  def drop(n)
    result = []
    i = 0
    each { |x| result << x if i >= n; i += 1 }
    result
  end

  def group_by
    h = {}
    each do |x|
      k = yield(x)
      (h[k] ||= []) << x
    end
    h
  end

  def partition
    yes = []
    no = []
    each { |x| (yield(x) ? yes : no) << x }
    [yes, no]
  end

  def tally
    h = {}
    each { |x| h[x] = (h[x] || 0) + 1 }
    h
  end

  def to_h
    h = {}
    each do |x|
      pair = block_given? ? yield(x) : x
      h[pair[0]] = pair[1]
    end
    h
  end

  def each_slice(n)
    slice = []
    each do |x|
      slice << x
      if slice.length == n
        yield(slice)
        slice = []
      end
    end
    yield(slice) unless slice.empty?
    self
  end

  def zip(*others)
    result = []
    each_with_index { |x, i| result << ([x] + others.map { |o| o[i] }) }
    result
  end

  def filter_map
    result = []
    each { |x| v = yield(x); result << v if v }
    result
  end
end
